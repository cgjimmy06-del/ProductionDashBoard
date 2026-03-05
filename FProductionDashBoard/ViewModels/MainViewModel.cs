using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FProductionDashBoard
{
    public partial class MainViewModel : ObservableObject
    {
        public ObservableCollection<DeviceCardViewModel> Devices { get; } = 
            new ObservableCollection<DeviceCardViewModel>();
        public LogViewModel Log { get; } = new LogViewModel();

        public ICommand AddDeviceCommand { get; }

        [ObservableProperty] 
        private int totalDevices; 
        [ObservableProperty] 
        private int totalButtonClicks;
        public MainViewModel() 
        { 
            AddDeviceCommand = new RelayCommand(AddDevice); 
        }
        private void AddDevice()
        {
            var vm = new AddDeviceViewModel();
            var window = new SubWindow1 { DataContext = vm };
            vm.OnConfirm += (info) =>
            {
                var device = new DeviceCardViewModel(info, Log); // 訂閱設備的點擊事件
                device.OnButtonClicked += UpdateStats;

                Devices.Add(device);
                UpdateStats();
                window.Close();

                Log.AddLog($"已新增設備: {info.Name}", LogLevel.Info);
            }; 
            vm.OnCancel += () => window.Close(); 
            window.ShowDialog();
        }
        private void UpdateStats() 
        { 
            TotalDevices = Devices.Count; 
            TotalButtonClicks = Devices.Sum(d => d.ButtonClickCount); 
        }
    }


}
