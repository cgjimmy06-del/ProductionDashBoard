using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.UserControls;
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
        private bool isCollapsedNav = false; // 導覽列收合
        [ObservableProperty]
        private string currentTime = ""; // 系統時間
        [ObservableProperty]
        private bool autoScrollEnabled = true; // 訊息視窗是否滾動
        [ObservableProperty]
        private bool isLargeFontMode = false; // 訊息視窗是否放大字型
        [ObservableProperty]
        private bool isErrorMode = false; // 訊息視窗是否切換至異常訊息
        [ObservableProperty]
        private int progressValue = 0; // 進度數值
        [ObservableProperty]
        private string progressString = Properties.Resources.MainProgressIdle; // 進度訊息
        
        public ObservableCollection<LogEntry> CurrentLogs => IsErrorMode ? _log.ErrorLogs : _log.Logs;
        
        // 導覽列
        public ICommand CollapseNavCommand { get; }
        // 訊息窗
        public ICommand SaveLogsCommand { get; }
        // 卡片區
        public ICommand DCardManageCommand { get; }

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

            // 設定元件事件 (導覽列)
            CollapseNavCommand = new RelayCommand(() => { IsCollapsedNav = !IsCollapsedNav; });

            //// 設定元件事件 (訊息視窗)
            SaveLogsCommand = new AsyncRelayCommand(() => SaveLogsAsync());

            // 設定元件事件 (設備卡片區)
            DCardManageCommand = new RelayCommand(() => AddDeviceCard());
        }
        partial void OnIsErrorModeChanged(bool value)
        {
            if (value && _log.IsNewErrorLog) _log.IsNewErrorLog = false;

            OnPropertyChanged(nameof(CurrentLogs));

            // 取代此函式 (不需判斷PropertyName)
            //PropertyChanged += (s, e) => {
            //    if (e.PropertyName == nameof(IsErrorMode)) OnPropertyChanged(nameof(CurrentLogs)); };
        }
        private async Task SaveLogsAsync() // 用於儲存訊息時非同步追蹤 (尚未建立按鈕鎖定)
        {
            try
            {
                ProgressString = Properties.Resources.MainProgressSaving;

                await _log.SaveAllLogsToFileAsync();

                ProgressString = Properties.Resources.MainProgressSuccess;
            }
            catch (AggregateException ex)
            {
                ProgressString = Properties.Resources.MainProgressStopped;
                _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: SaveLogsCommand");
                _log.AddErrorLog($"SaveLogsCommand Aggre.Ex: {ex.ToString()}");
            }
            catch (Exception ex)
            {
                // 最外層保護，抓所有未預期的錯誤
                ProgressString = Properties.Resources.MainProgressStopped;
                _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: SaveLogsCommand");
                _log.AddErrorLog($"SaveLogsCommand Ex: {ex.ToString()}");
            }
        }
        private async void AddDeviceCard()
        {
            var vm = new DCManageDialogViewModel(Properties.Resources.DeviceCardManageDialog, 
                                                    _log, _sqlService, Devices.ToList()); // 待翻譯
            await vm.InitAsync();
            var uc = new DCardManageDialog { DataContext = vm };
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
        private async void SqlTestFunc() 
        {
            try
            {
                _log.AddLog($"連線狀態: {_sqlService.DeviceRepo.CheckConnection()}");

                var devs = await _sqlService.DeviceRepo.GetAllAsync();
                foreach (var dev in devs) { _log.AddLog($"已新增設備: {dev.Name}", LogLevel.Info); }
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




    }


}
