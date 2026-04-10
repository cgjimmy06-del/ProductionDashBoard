using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.Repositories;
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
        private List<DeviceInfo> sqlDevicesList = new(); // 設備清單 (資料表來源)
        public ObservableCollection<DeviceCardViewModel> Devices { get; } =
            new ObservableCollection<DeviceCardViewModel>();

        private readonly LogService _log;
        private readonly SqlService _sqlService;
        public UserInfo CurrentUser { get; }

        public ICommand FirstArticleInsAllCommand { get; }
        public ICommand RoutineInsAllCommand { get; }
        public ICommand AddDevicesCommand { get; }
        public ICommand FastDownloadDevicesCommand { get; }
        public ICommand FastUploadDevicesCommand { get; }


        public DeviceCardContainerViewModel(LogService log, SqlService sqlservice, UserInfo currentuser)
        {
            _log = log;
            _sqlService = sqlservice;
            CurrentUser = currentuser;

            FirstArticleInsAllCommand = new RelayCommand(() => FirstArticleInsAll());
            RoutineInsAllCommand = new RelayCommand(() => RoutineInsAll());

            AddDevicesCommand = new AsyncRelayCommand(() => AddDeviceCard());
            FastDownloadDevicesCommand = new RelayCommand(() => FastDownloadDevices());
            FastUploadDevicesCommand = new RelayCommand(() => FastUploadDevices());
        }
        public async Task GetListsFromSqlAsync()
        {
            try
            {
                if (!_sqlService.EquipmentRep.CheckConnection()) // 待翻譯log 並加上errorlog
                    _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: Check connection error", LogLevel.Error);

                var equipmentList = (await _sqlService.EquipmentRep.GetAllAsync());

                if (sqlDevicesList.Any()) sqlDevicesList.Clear();
                foreach (var eq in equipmentList)
                    sqlDevicesList.Add(new DeviceInfo {
                        Id = eq.Id, DeviceID = eq.EquipmentId, Name = eq.Name,
                        IP = eq.Ip, Port = eq.Port,
                        Factory = eq.Factory, Building = eq.Building, Floor = eq.Floor,
                        Description = eq.Description
                    });
            }
            catch (SqlException sqlex)
            {
                Debug.WriteLine($"SqlException: {sqlex.Message}");
            }
            catch (TaskCanceledException taskex)
            {
                Debug.WriteLine($"TaskCanceledException: {taskex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}");
            }
        }

        private void FirstArticleInsAll()
        {
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceFirstInsDialog, Devices[0]);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new() { IsNormal = false };
                foreach (var idevice in Devices)
                    idevice.InspectionStatuses.updateFirstInspection(result.IsNormal, result.Description);
            }
        }
        private void RoutineInsAll()
        {
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceRoutineInsDialog, Devices[0]);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new InspectionResult() { IsNormal = false };
                int statusresult = result.IsNormal ? 0 : 2;
                foreach (var idevice in Devices)
                    idevice.InspectionStatuses.updateRoutineStatus(statusresult, result.Description);
            }
        }

        private async Task AddDeviceCard()
        {
            await GetListsFromSqlAsync(); // 可評估是否外部呼叫
            var vm = new AddDeivceDialogViewModel(Properties.Resources.DeviceCardManageDialog, defaultDevicesFile, 
                                                    sqlDevicesList, [.. Devices]); // [.. X] = X.ToList()
            var uc = new AddDeviceDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new DeviceCardsResult { Selections = new List<DeviceInfo>() };
                foreach (var iselection in result.Selections)
                {
                    var idevice = new DeviceCardViewModel(iselection, CurrentUser, _log, _sqlService);
                    Devices.Add(idevice);
                    _log.AddLog($"{Properties.Resources.ComStrAdded}: {iselection.Name}", LogLevel.Info);
                }
            }
        }
        private void FastDownloadDevices()
        {
            DataStorageService.Save(Devices.Select(s => s.Info), defaultDevicesFile);
            _log.AddLog($"{Properties.Resources.ComStrDownloaded}: {defaultDevicesFile}", LogLevel.Info);
        }
        private void FastUploadDevices()
        {
            var result = DataStorageService.Load<List<DeviceInfo>>(defaultDevicesFile).AsEnumerable();
            if (Devices.Any())
            {
                var existedIds = Devices.Select(s => s.Info.DeviceID).ToHashSet();
                result = result.Where(d => !existedIds.Contains(d.DeviceID));
            }
            foreach (var iselection in result)
            {
                var idevice = new DeviceCardViewModel(iselection, CurrentUser, _log, _sqlService);
                Devices.Add(idevice);
                _log.AddLog($"{Properties.Resources.ComStrAdded}: {iselection.Name}", LogLevel.Info);
            }
        }

    }
}
