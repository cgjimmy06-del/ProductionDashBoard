using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FProductionDashBoard
{
    public partial class MainViewModel : ObservableObject
    {
        public string AppVersion { get; }

        public ObservableCollection<DeviceCardViewModel> Devices { get; } = 
            new ObservableCollection<DeviceCardViewModel>();

        private readonly Repositories.IDeviceRepository _deviceRepo;
        public LogViewModel Log { get; } = new LogViewModel();

        public ICommand AddDeviceCommand { get; }

        [ObservableProperty] 
        private int totalDevices; 
        [ObservableProperty] 
        private int totalButtonClicks;
        public MainViewModel(Repositories.IDeviceRepository deviceRepo) 
        {
            // 讀取 FileVersion
            AppVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion ?? "Unknown";

            _deviceRepo = deviceRepo;

            AddDeviceCommand = new RelayCommand(() => AddDevice()); 
        }

        public void SaveDefault() // 可用在code-behind的closing
        {
            // 儲存參數
            //Properties.Settings.Default.Save();

            // 儲存集合
            //DataStorageService.SaveDevices(Devices);
        }


        private void AddDevice()
        {
            //try
            //{
            //    Log.AddLog($"連線狀態: {_deviceRepo.CheckConnection().ToString()}");
            //    Log.AddLog($"連線字串: {_deviceRepo.CurrectConnStr}");

            //    var devs = await _deviceRepo.GetAllAsync();
            //    foreach (var dev in devs) { Log.AddLog($"已新增設備: {dev.Name}", LogLevel.Info); }
            //}
            //catch (SqlException ex)
            //{
            //    Debug.WriteLine($"SQL 錯誤: {ex.Message}");
            //}
            //catch (TaskCanceledException)
            //{
            //    Debug.WriteLine("查詢已超時");
            //}
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
