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
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FProductionDashBoard.Models;

namespace FProductionDashBoard.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public string AppVersion { get; }
        public DispatcherTimer DefaultTimer;

        public ObservableCollection<DeviceCardViewModel> Devices { get; } = 
            new ObservableCollection<DeviceCardViewModel>();

        // 資源 DI注入
        private readonly SqlService _sqlService;
        public LogService _log { get; }
        public UserInfo SystemUser { get; }
        public UserInfo CurrentUser { get; }

        // 介面邏輯
        [ObservableProperty]
        private bool isCollapsed = false; // 導覽列收合
        [ObservableProperty]
        private string currentTime = ""; // 系統時間
        [ObservableProperty]
        private bool autoScrollEnabled = true; // 訊息視窗是否滾動
        [ObservableProperty]
        private int progressValue = 0; // 進度數值
        [ObservableProperty]
        private string progressString = Properties.Resources.MainProgressIdle; // 進度訊息

        // 註冊介面
        public ICommand AddDeviceCommand { get; }
        public ICommand CollapseNavCommand { get; }

        public MainViewModel(UserInfo user, LogService log, SqlService sqlservice) 
        {
            // 讀取 FileVersion
            AppVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion ?? "Unknown";

            // 建立 DispatcherTimer 每秒更新一次時間
            DefaultTimer = new DispatcherTimer();
            DefaultTimer.Interval = TimeSpan.FromSeconds(1);
            DefaultTimer.Tick += (s, e) =>
            { CurrentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm"); };
            DefaultTimer.Start();

            // DI注入 Repository
            SystemUser = user;
            CurrentUser = user;
            _log = log;
            _sqlService = sqlservice;

            // 設定元件事件
            CollapseNavCommand = new RelayCommand(() => { IsCollapsed = !IsCollapsed; });
            AddDeviceCommand = new RelayCommand(() => AddDevice());
        }

        private void AddDevice()
        {
            var vm = new AddDeviceViewModel();
            var window = new SubWindow1 { DataContext = vm };
            vm.OnConfirm += (info) =>
            {
                var device = new DeviceCardViewModel(info, CurrentUser, _log);

                Devices.Add(device);
                window.Close();

                _log.AddLog($"已新增設備: {info.Name}", LogLevel.Info);
            };
            vm.OnCancel += () => window.Close();
            window.ShowDialog();
        }
        private void UpdateStats() 
        {
            // TotalDevices = Devices.Count; 
            // TotalButtonClicks = Devices.Sum(d => d.ButtonClickCount);

            //try
            //{
            //    _log.AddLog($"連線狀態: {_sqlService.DeviceRepo.CheckConnection()}");

            //    var devs = await _sqlService.DeviceRepo.GetAllAsync();
            //    foreach (var dev in devs) { _log.AddLog($"已新增設備: {dev.Name}", LogLevel.Info); }
            //}
            //catch (SqlException ex)
            //{
            //    Debug.WriteLine($"SQL 錯誤: {ex.Message}");
            //}
            //catch (TaskCanceledException)
            //{
            //    Debug.WriteLine("查詢已超時");
            //}
        }




    }


}
