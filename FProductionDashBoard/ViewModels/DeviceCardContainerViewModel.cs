using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.Services;
using FProductionDashBoard.UserControls;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class DeviceCardContainerViewModel : ObservableObject
    {
        private string defaultDevicesFile = "defaultdevices.json"; // 預設設備檔案
        private readonly ListsFormSql commonLists;
        public ObservableCollection<DeviceCardViewModel> Devices { get; } =
            new ObservableCollection<DeviceCardViewModel>();

        private readonly LogService _log;
        private readonly IDataService _dataService;
        public UserInfo CurrentUser { get; }

        public ICommand FirstArticleInsAllCommand { get; }
        public ICommand RoutineInsAllCommand { get; }
        public ICommand AddDevicesCommand { get; }
        public ICommand FastDownloadDevicesCommand { get; }
        public ICommand FastUploadDevicesCommand { get; }

        public DeviceCardContainerViewModel(LogService log, IDataService dataservice, UserInfo currentuser, ListsFormSql getlists)
        {
            _log = log;
            _dataService = dataservice;
            CurrentUser = currentuser;
            commonLists = getlists;

            FirstArticleInsAllCommand = new RelayCommand(() => FirstArticleInsAll());
            RoutineInsAllCommand = new RelayCommand(() => RoutineInsAll());

            AddDevicesCommand = new AsyncRelayCommand(() => AddDeviceCard());
            FastDownloadDevicesCommand = new RelayCommand(() => FastDownloadDevices());
            FastUploadDevicesCommand = new AsyncRelayCommand(() => FastUploadDevices());
        }
        // 待翻譯
        private async void FirstArticleInsAll()
        {
            if (!Devices.Any()) return;

            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceFirstInsDialog, Devices[0],
                commonLists.ErrorsList);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();

                    foreach (var idevice in Devices)
                    {
                        await _dataService.AddFirstInspectionAsync(idevice.Info.Id, idevice.CurrentUser.Id, result.IsNormal,
                        idevice.CurrentProduct.Name, result.ErrorCode, result.Description);

                        idevice.FirstInspectionStatus = result.IsNormal;
                    }
                }
                catch (Exception ex)
                {
                    _log.AddLog("首件紀錄上傳異常 (批次)");
                    _log.AddErrorLog($"FirstArticleInspection: {ex.Message}");
                }
            }
        }
        private async void RoutineInsAll()
        {
            if (!Devices.Any()) return;

            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceFirstInsDialog, Devices[0],
                commonLists.ErrorsList);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();

                    var currentTimeSlot = await _dataService.GetCurrentTimeSlotIdAsync();
                    if (currentTimeSlot == null)
                    {
                        _log.AddLog("目前不在任何巡檢時段內");
                        return;
                    }

                    foreach (var idevice in Devices)
                    {
                        await _dataService.AddRoutineInspectionAsync(idevice.Info.Id, idevice.CurrentUser.Id, result.IsNormal,
                            currentTimeSlot ?? 1, idevice.CurrentProduct.Name, result.ErrorCode, result.Description);
                        await idevice.UpdateTimeSlotsStatusAsync();
                    }
                }
                catch(SqlException sqlex)
                {
                    _log.AddLog("巡檢紀錄上傳異常 (批次)");
                    _log.AddErrorLog($"RoutineInspection: {sqlex.Message}");
                }
                catch (Exception ex)
                {
                    _log.AddLog("巡檢紀錄上傳異常 (批次)");
                    _log.AddErrorLog($"RoutineInspection: {ex.Message}");
                }
            }
        }

        private async Task AddDeviceCard()
        {
            var vm = new AddDeivceDialogViewModel(Properties.Resources.DeviceCardManageDialog, defaultDevicesFile,
                                                    commonLists.DevicesList, [.. Devices]); // [.. X] = X.ToList()
            var uc = new AddDeviceDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new();
                foreach (var iselection in result.Selections)
                {
                    var idevice = new DeviceCardViewModel(iselection, CurrentUser, _log, _dataService, commonLists);
                    await idevice.UpdateTimeSlotsStatusAsync();
                    Devices.Add(idevice);
                    _log.AddLog($"{Properties.Resources.ComStrAdded}: {iselection.Name}", LogLevel.Info);
                }
            }
        }
        private void FastDownloadDevices()
        {
            JsonDataService.Save(Devices.Select(s => s.Info), defaultDevicesFile);
            _log.AddLog($"{Properties.Resources.ComStrDownloaded}: {defaultDevicesFile}", LogLevel.Info);
        }
        private async Task FastUploadDevices()
        {
            var result = JsonDataService.Load<List<DeviceInfo>>(defaultDevicesFile).AsEnumerable();
            if (Devices.Any())
            {
                var existedIds = Devices.Select(s => s.Info.DeviceID).ToHashSet();
                result = result.Where(d => !existedIds.Contains(d.DeviceID));
            }
            foreach (var iselection in result)
            {
                var idevice = new DeviceCardViewModel(iselection, CurrentUser, _log, _dataService, commonLists);
                await idevice.UpdateTimeSlotsStatusAsync();
                Devices.Add(idevice);
                _log.AddLog($"{Properties.Resources.ComStrAdded}: {iselection.Name}", LogLevel.Info);
            }
        }

    }
}
