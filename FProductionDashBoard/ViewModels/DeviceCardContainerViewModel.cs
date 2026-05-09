using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.Exceptions;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.UserControls;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class DeviceCardContainerViewModel : ObservableObject, IDisposable
    {
        private string defaultDevicesFile = "defaultdevices.json"; // 預設設備檔案
        private readonly ListsFromSql commonLists;
        public ObservableCollection<DeviceCardViewModel> Devices { get; } =
            new ObservableCollection<DeviceCardViewModel>();

        private readonly DashboardCoreServices _core;
        private readonly Services.IDialogService _dialog;
        private readonly Action _onUserChanged; // 可被取消註冊
        public UserInfo? CurrentUser => _core.Authorization.CurrentUser;

        public ICommand FirstArticleInsAllCommand { get; }
        public ICommand RoutineInsAllCommand { get; }
        public ICommand AddDevicesCommand { get; }
        public ICommand FastDownloadDevicesCommand { get; }
        public ICommand FastUploadDevicesCommand { get; }
        public ICommand DeleteDevicesCommand { get; }

        public DeviceCardContainerViewModel(DashboardCoreServices core, Services.IDialogService dialog, ListsFromSql getlists)
        {
            _core = core;
            _dialog = dialog;
            commonLists = getlists;

            FirstArticleInsAllCommand = new AsyncRelayCommand(() => FirstArticleInsAll());
            RoutineInsAllCommand = new AsyncRelayCommand(() => RoutineInsAll());

            AddDevicesCommand = new AsyncRelayCommand(() => AddDeviceCard());
            FastDownloadDevicesCommand = new RelayCommand(() => FastDownloadDevices());
            FastUploadDevicesCommand = new AsyncRelayCommand(() => FastUploadDevices());
            DeleteDevicesCommand = new RelayCommand(() => DeleteDevices());

            _onUserChanged = () => OnPropertyChanged(nameof(CurrentUser));
            _core.Authorization.UserChanged += _onUserChanged;
        }
        // 待翻譯
        private async Task FirstArticleInsAll()
        {
            if (!Devices.Any()) return;

            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceFirstInsDialog, Devices[0],
                commonLists.ErrorsList);
            _dialog.ShowDialog(vm);

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new();

                foreach (var idevice in Devices)
                {
                    try
                    {
                        idevice.FirstInspectionStatus = result.IsNormal;

                        await _core.Data.AddFirstInspectionAsync(idevice.Info.Id, CurrentUser!.Id, result.IsNormal,
                        idevice.CurrentProduct.Name, result.ErrorCode, result.Description);

                        _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{idevice.Info.Name} - " +
                        $"首件紀錄上傳完成");
                    }
                    catch (OfflineOperationQueuedException)
                    {
                        _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{idevice.Info.Name} - " +
                            $"首件紀錄已暫存，待連線恢復後自動上傳", LogLevel.Warning);
                    }
                    catch (BusinessRuleException ex)
                    {
                        _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{idevice.Info.Name} - " +
                        $"業務規則異常", LogLevel.Error);
                        _core.Log.AddErrorLog($"FirstInspection BusinessRuleEx: {ex.Message}");
                        idevice.FirstInspectionStatus = false;
                    }
                    catch (Exception ex)
                    {
                        _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{idevice.Info.Name} - " +
                            $"首件紀錄上傳異常");
                        _core.Log.AddErrorLog($"FirstInspection Ex: {ex.Message}");
                        idevice.FirstInspectionStatus = false;
                    }

                }
            }
        }
        private async Task RoutineInsAll()
        {
            if (!Devices.Any()) return;

            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceFirstInsDialog, Devices[0],
                commonLists.ErrorsList);
            _dialog.ShowDialog(vm);

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new();

                var currentTimeSlot = _core.Data.GetCurrentTimeSlotId(commonLists.TimeSlotsList);
                if (currentTimeSlot == null)
                {
                    _core.Log.AddLog("目前不在任何巡檢時段內");
                    return;
                }

                foreach (var idevice in Devices)
                {
                    try
                    {
                        await _core.Data.AddRoutineInspectionAsync(idevice.Info.Id, CurrentUser!.Id, result.IsNormal,
                            currentTimeSlot ?? 1, idevice.CurrentProduct.Name, result.ErrorCode, result.Description);
                        await idevice.UpdateTimeSlotsStatusAsync();

                        _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{idevice.Info.Name} - " +
                        $"巡檢紀錄上傳完成");
                    }
                    catch (OfflineOperationQueuedException)
                    {
                        _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{idevice.Info.Name} - " +
                            $"巡檢紀錄已暫存，待連線恢復後自動上傳", LogLevel.Warning);
                    }
                    catch (BusinessRuleException ex)
                    {
                        _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{idevice.Info.Name} - " +
                            $"業務規則異常", LogLevel.Error);
                        _core.Log.AddErrorLog($"RoutineInspection BusinessRuleEx: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{idevice.Info.Name} - " +
                            $"巡檢紀錄上傳異常");
                        _core.Log.AddErrorLog($"RoutineInspection Ex: {ex.Message}");
                    }
                }
            }
        }

        private async Task AddDeviceCard()
        {
            var vm = new AddDeviceDialogViewModel(Properties.Resources.DeviceCardManageDialog, defaultDevicesFile,
                                                    commonLists.DevicesList, [.. Devices]); // [.. X] = X.ToList()
            _dialog.ShowDialog(vm);

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new();
                foreach (var iselection in result.Selections)
                {
                    var idevice = new DeviceCardViewModel(_core, _dialog, iselection, CurrentUser!, commonLists);
                    await idevice.UpdateTimeSlotsStatusAsync();
                    Application.Current.Dispatcher.Invoke(() => Devices.Add(idevice));
                    _core.Log.AddLog($"{Properties.Resources.ComStrAdded}: {iselection.Name}", LogLevel.Info);
                }
            }
        }
        private void FastDownloadDevices()
        {
            JsonDataService.Save(Devices.Select(s => s.Info), defaultDevicesFile);
            _core.Log.AddLog($"{Properties.Resources.ComStrDownloaded}: {defaultDevicesFile}", LogLevel.Info);
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
                var idevice = new DeviceCardViewModel(_core, _dialog, iselection, CurrentUser!, commonLists);
                await idevice.UpdateTimeSlotsStatusAsync();
                Application.Current.Dispatcher.Invoke(() => Devices.Add(idevice));
                _core.Log.AddLog($"{Properties.Resources.ComStrAdded}: {iselection.Name}", LogLevel.Info);
            }
        }
        private void DeleteDevices()
        {
            if (Devices.Any()) Application.Current.Dispatcher.Invoke(() => Devices.Clear());
        }

        public void Dispose()
        {
            _core.Authorization.UserChanged -= _onUserChanged;
        }
    }
}
