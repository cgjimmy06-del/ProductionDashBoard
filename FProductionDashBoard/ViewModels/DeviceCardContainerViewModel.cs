using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
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
        public ObservableCollection<DeviceCardViewModel> Devices { get; } =
            new ObservableCollection<DeviceCardViewModel>();

        private readonly LogService _log;
        private readonly SqlService _sqlService;
        public UserInfo CurrentUser { get; }

        public ICommand AddDevicesCommand { get; }
        public ICommand FastDownloadDevicesCommand { get; }
        public ICommand FastUploadDevicesCommand { get; }


        public DeviceCardContainerViewModel(LogService log, SqlService sqlservice, UserInfo currentuser)
        {
            _log = log;
            _sqlService = sqlservice;
            CurrentUser = currentuser;

            AddDevicesCommand = new RelayCommand(() => AddDeviceCard());
            FastDownloadDevicesCommand = new RelayCommand(() => FastDownloadDevices());
            FastUploadDevicesCommand = new RelayCommand(() => FastUploadDevices());

        }
        private async void AddDeviceCard()
        {
            var vm = new AddDeivceDialogViewModel(Properties.Resources.DeviceCardManageDialog, 
                                                    Devices.ToList(), defaultDevicesFile);
            await vm.InitAsync(_sqlService); // try catch 提取至此 (待修正-查詢後注入清單(建構)，SQL統一在Container操作)
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
        private void FastDownloadDevices() // 不依賴 view model
        {
            DataStorageService.Save(Devices.Select(s => s.Info), defaultDevicesFile);
            _log.AddLog($"{Properties.Resources.ComStrDownloaded}: {defaultDevicesFile}", LogLevel.Info);
        }
        private void FastUploadDevices() // 不依賴 view model
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
