using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Repositories;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
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

        // 資源 DI注入
        private readonly SqlService _sqlService;
        public LogService _log { get; }
        public UserInfo _user { get; }

        private readonly PaletteHelper _paletteHelper = new PaletteHelper();
        private readonly Theme? _lightTheme;
        //private readonly Theme? _darkTheme;

        public ICommand AddDeviceCommand { get; }

        public MainViewModel(UserInfo user, LogService log, SqlService sqlservice) 
        {
            // 讀取 FileVersion
            AppVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion ?? "Unknown";

            // DI注入 Repository
            _user = user;
            _log = log;
            _sqlService = sqlservice;

            // 設定元件事件
            AddDeviceCommand = new RelayCommand(() => AddDevice());

            // 主題顏色設定
            _lightTheme = Theme.Create(BaseTheme.Light,
               SwatchHelper.Lookup[MaterialDesignColor.Indigo],
               SwatchHelper.Lookup[MaterialDesignColor.Lime]);
            //_lightTheme.SetBaseTheme(BaseTheme.Light); // 設定基本主題
            //_lightTheme.SetPrimaryColor(SwatchHelper.Lookup[MaterialDesignColor.Red]); // 設定主色系
            //_lightTheme.SetSecondaryColor(SwatchHelper.Lookup[MaterialDesignColor.Green]); // 設定次色系
            _paletteHelper.SetTheme(_lightTheme);

        }

        public void SaveDefault() // 可用在code-behind的closing
        {
            // 儲存參數
            //Properties.Settings.Default.Save();

            // 儲存集合
            //DataStorageService.SaveDevices(Devices);
        }


        private async void AddDevice()
        {
            try
            {
                _log.AddLog($"連線狀態: {_sqlService.DeviceRepo.CheckConnection()}");

                var devs = await _sqlService.DeviceRepo.GetAllAsync();
                foreach (var dev in devs) { _log.AddLog($"已新增設備: {dev.Name}", LogLevel.Info); }
            }
            catch (SqlException ex)
            {
                Debug.WriteLine($"SQL 錯誤: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("查詢已超時");
            }
            //var vm = new AddDeviceViewModel();
            //var window = new SubWindow1 { DataContext = vm };
            //vm.OnConfirm += (info) =>
            //{
            //    var device = new DeviceCardViewModel(info, _log);
            //    device.OnButtonClicked += UpdateStats; // 訂閱設備的點擊事件

            //    Devices.Add(device);
            //    UpdateStats();
            //    window.Close();

            //    _log.AddLog($"已新增設備: {info.Name}", LogLevel.Info);
            //};
            //vm.OnCancel += () => window.Close();
            //window.ShowDialog();
        }
        private void UpdateStats() 
        { 
            // TotalDevices = Devices.Count; 
            // TotalButtonClicks = Devices.Sum(d => d.ButtonClickCount);

        }
    }


}
