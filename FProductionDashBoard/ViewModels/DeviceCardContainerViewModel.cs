using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.UserControls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class DeviceCardContainerViewModel : ObservableObject
    {
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
                                                    _log, _sqlService, Devices.ToList());
            await vm.InitAsync();
            var uc = new AddDeviceDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new DeviceCardsResult { Selections = new List<DeviceInfo>() };
                foreach (var iselection in result.Selections)
                {
                    var idevice = new DeviceCardViewModel(iselection, CurrentUser, _log);
                    Devices.Add(idevice);
                    _log.AddLog($"{Properties.Resources.ComStrAdded}: {iselection.Name}", LogLevel.Info);
                }
            }
        }
        private void FastDownloadDevices() // 可能須重構 (檔案名稱/view model來源)
        {
            DataStorageService.Save(Devices.Select(s => s.Info), "defaultdevices.json");
            _log.AddLog($"{Properties.Resources.ComStrDownloaded}: defaultdevices.json", LogLevel.Info);
        }
        private void FastUploadDevices() // 可能須重構 (檔案名稱/view model來源)
        {
            var result = DataStorageService.Load<List<DeviceInfo>>("defaultdevices.json");
            foreach (var iselection in result)
            {
                var idevice = new DeviceCardViewModel(iselection, CurrentUser, _log);
                Devices.Add(idevice);
                _log.AddLog($"{Properties.Resources.ComStrAdded}: {iselection.Name}", LogLevel.Info);
            }
        }

    }
}
