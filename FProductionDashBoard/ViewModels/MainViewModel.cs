using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.Services;
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
    public enum NavMode { Home, Operation, List, View }
    public partial class MainViewModel : ObservableObject
    {
        public string AppVersion { get; }
        public DispatcherTimer DefaultTimer;

        public ObservableCollection<object> Cards { get; set; } = new();
        [ObservableProperty]
        public object? mainCard; // 主視窗中的主卡片
        private DeviceCardContainerViewModel? _deviceContainer;

        #region  -- DI注入資源 --
        private readonly IDataService _dataService;
        private readonly IOfflineSyncService _syncService;
        public LogService _log { get; } // public 是為了Window的顯示
        private AuthorizationService _authService { get; }
        public UserInfo? SystemUser => _authService.CurrentUser;
        public UserInfo? CurrentUser => _authService.CurrentUser;
        public bool IsLoggedIn => _authService.IsLoggedIn;
        #endregion
        #region -- 介面邏輯 --
        [ObservableProperty]
        private int businessHour = 8; // 定義工作天的時
        [ObservableProperty]
        private int businessMinute = 0; // 定義工作天的分
        [ObservableProperty]
        private string currentTime = ""; // 系統時間
        [ObservableProperty]
        private bool isCollapsedNav = false; // 導覽列收合
        [ObservableProperty]
        private bool autoScrollEnabled = true; // 訊息視窗是否滾動
        [ObservableProperty]
        private bool isLargeFontMode = false; // 訊息視窗是否放大字型
        [ObservableProperty]
        private bool isErrorMode = false; // 訊息視窗是否切換至異常訊息
        [ObservableProperty]
        private int progressValue = 0;
        [ObservableProperty]
        private string progressString = Properties.Resources.MainProgressIdle;
        [ObservableProperty]
        private bool isProgressIndeterminate = false;
        [ObservableProperty]
        private bool isProgressVisible = false;
        [ObservableProperty]
        private NavMode currentNavMode = NavMode.Home; // 當前導覽列模式
        #endregion

        public ObservableCollection<LogEntry> CurrentLogs => IsErrorMode ? _log.ErrorLogs : _log.Logs;
        public ListsFromSql CommonLists = new();

        public ICommand InitializeCommand { get; }
        // 菜單列

        // 狀態列
        public IAsyncRelayCommand LoginCommand { get; } // 登入事件
        public IAsyncRelayCommand LogoutCommand { get; } // 登出事件

        // 工具列
        public IRelayCommand TestCommand { get; }
        // 導覽列
        public ICommand CollapseNavCommand { get; }
        public IRelayCommand SwitchModeCommand { get; }
        // 訊息窗
        public ICommand SaveLogsCommand { get; }
        // 主視覺視窗

        private int _syncTickCounter = 0;
        private int _missedCheckCounter = 0;
        private bool _isSyncing = false;

        public MainViewModel(LogService log, IDataService dataservice, AuthorizationService auth,
            IOfflineSyncService syncService)
        {
            // 讀取 FileVersion
            AppVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion ?? "Unknown";

            // DI注入 Repository
            _log = log;
            _dataService = dataservice;
            _authService = auth;
            _syncService = syncService;

            // 定義工作起始時間 (於 DispatcherTimer 偵測更新)
            _dataService.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);

            // 建立 DispatcherTimer 每秒更新一次時間，此方法位於 "UI 執行緒" 需確認是否移至 "執行緒池"
            DefaultTimer = new DispatcherTimer();
            DefaultTimer.Interval = TimeSpan.FromSeconds(1);
            DefaultTimer.Tick += async (s, e) => await OnTimerTickAsync();
            DefaultTimer.Start();

            InitializeCommand = new AsyncRelayCommand(LoadAllListsAsync);
            // 設定元件事件 (導覽列)
            CollapseNavCommand = new RelayCommand(() => { IsCollapsedNav = !IsCollapsedNav; });
            SwitchModeCommand = new RelayCommand<NavMode>(SwitchMode,
                (NavMode) => _authService.HasPermission(Services.PermissionId.View));

            //  設定元件事件 (工具列)
            TestCommand = new AsyncRelayCommand(() => SqlTestFunc(),
                () => _authService.HasPermission(Services.PermissionId.Test));

            // 設定元件事件 (帳號) 
            LoginCommand = new AsyncRelayCommand(LoginAsync);
            LogoutCommand = new AsyncRelayCommand(() => _authService.LogoutAsync());

            // 設定元件事件 (訊息視窗)
            SaveLogsCommand = new AsyncRelayCommand(() => SaveLogsAsync());

            // (訂閱端) 使用者變更事件，刷新命令狀態與使用者顯示 (若 _authService 在執行緒池)
            _authService.UserChanged += () => System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!_authService.IsLoggedIn) // 若登出則執行對應事件
                    _deviceContainer = null;

                SwitchModeCommand.NotifyCanExecuteChanged();
                TestCommand.NotifyCanExecuteChanged();
                LogoutCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(SystemUser));
                OnPropertyChanged(nameof(CurrentUser));
                OnPropertyChanged(nameof(IsLoggedIn));
            });

            // 新增儀表卡片區
            //Cards.Add(new DeviceCardContainerViewModel(_log, _dataService, CurrentUser));

        }

        #region -- 載入初始化 與 計時器 -- (待翻譯log 並加上errorlog)
        public async Task LoadAllListsAsync()
        {
            var t1 = FetchListAsync(() => _dataService.GetDevicesAsync());
            var t2 = FetchListAsync(() => _dataService.GetUsersAsync());
            var t3 = FetchListAsync(() => _dataService.GetMaterialsAsync());
            var t4 = FetchListAsync(() => _dataService.GetErrorsAsync(Properties.Settings.Default.CultureCode));
            var t5 = FetchListAsync(() => _dataService.GetTimeSlotsAsync());
            var t6 = FetchListAsync(() => _dataService.GetAllRolesAsync());
            await Task.WhenAll(t1, t2, t3, t4, t5, t6);

            CommonLists = new ListsFromSql
            {
                DevicesList  = t1.Result,
                UsersList    = t2.Result,
                MaterialsList = t3.Result,
                ErrorsList   = t4.Result,
                TimeSlotsList = t5.Result,
                RolesList    = t6.Result
            };
            _log.AddLog($"已載入清單: " +
                $"Devices:[{CommonLists.DevicesList.Count}]-" +
                $"Users:[{CommonLists.UsersList.Count}]-" +
                $"Materials:[{CommonLists.MaterialsList.Count}]-" +
                $"Errors:[{CommonLists.ErrorsList.Count}]-" +
                $"TimeSlots:[{CommonLists.TimeSlotsList.Count}]-" +
                $"Roles:[{CommonLists.RolesList.Count}]");

            await SyncAndLogAsync();
            await CheckMissedInspectionsAsync();
        }
        private async Task<List<T>> FetchListAsync<T>(Func<Task<List<T>>> fetch)
        {
            try { return await fetch(); }
            catch (Exception ex)
            {
                _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: {ex.Message}", LogLevel.Error);
                return [];
            }
        }
        // Timer Tick 自動偵測邏輯
        private async Task OnTimerTickAsync()
        {
            CurrentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            if (DateTime.Now.AddDays(-1) > _dataService.BusinessDay)
                _dataService.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);

            _syncTickCounter++;
            if (_syncTickCounter >= 30)
            {
                _syncTickCounter = 0;
                await SyncAndLogAsync();
            }

            _missedCheckCounter++;
            if (_missedCheckCounter >= 300)
            {
                _missedCheckCounter = 0;
                await CheckMissedInspectionsAsync();
            }
        }
        // 未巡檢偵測與插入 -- 巡檢狀態更新 (若離線狀態延至下個工作日，則前日未插入之資料將會遺漏) ** 
        private async Task CheckMissedInspectionsAsync()
        {
            var activeDevices = _deviceContainer?.Devices;
            if (activeDevices == null || !activeDevices.Any()) return;
            if (CommonLists.TimeSlotsList.Count == 0) return;

            foreach (var card in activeDevices)
            {
                try
                {
                    await _dataService.CheckAndInsertMissedInspectionAsync(
                        CommonLists.TimeSlotsList, card.Info.Id);

                    await card.UpdateTimeSlotsStatusAsync(); // 更新每台設備的巡檢狀態
                }
                catch (InvalidOperationException)
                {
                    _log.AddLog("逾時補填/狀態更新略過：資料庫連線失敗", LogLevel.Warning);
                    break;
                }
                catch (Exception ex)
                {
                    _log.AddLog($"逾時補填失敗 [{card.Info.Name}]: {ex.Message}", LogLevel.Error);
                }
            }
        }
        // 同步暫存資料
        private async Task SyncAndLogAsync()
        {
            if (_isSyncing) return;
            _isSyncing = true;
            try
            {
                var result = await _syncService.SyncPendingAsync();
                // TODO: 根據 result.SyncedCount / result.FailedCount 決定 log 輸出時機
                if (result?.SyncedCount > 0)
                	_log.AddLog($"已重新連線: 上傳{result?.SyncedCount}筆暫存資料");
            	if (result?.FailedCount > 0)
                	_log.AddLog($"連線失敗: {result?.FailedCount}筆資料等待上傳", LogLevel.Warning);
            }
            finally
            { _isSyncing = false; }
        }
        #endregion

        // 工具列 與 狀態列 事件
        private async Task LoginAsync()
        {
            var loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() != true) { return; }

            await _authService.InitializeAsync(loginWindow.User);
        }
        private void SetProgress(string message, bool visible = true, bool indeterminate = false, int value = 0)
        {
            ProgressString = message;
            IsProgressVisible = visible;
            IsProgressIndeterminate = indeterminate;
            ProgressValue = value;
        }
        private void ClearProgress() => SetProgress(Properties.Resources.MainProgressIdle, visible: false);
        // 導覽列事件
        public void SwitchMode(NavMode mode)
        {
            if (mode.Equals(CurrentNavMode)) return;
            CurrentNavMode = mode;
            switch (mode)
            {
                case NavMode.Home:
                    break;

                case NavMode.Operation:
                    _deviceContainer ??= new DeviceCardContainerViewModel(
                        _log, _dataService, _authService, CurrentUser!, CommonLists);
                    MainCard = _deviceContainer;
                    break;

                case NavMode.View:
                    break;

                case NavMode.List:
                    MainCard = new SettingViewModel(_log, _dataService, _authService);
                    break;

                default:
                    break;
            }
        }

        // 訊息窗事件
        partial void OnIsErrorModeChanged(bool value)
        {
            if (value && _log.IsNewErrorLog) _log.IsNewErrorLog = false;

            OnPropertyChanged(nameof(CurrentLogs));
            // 取代此函式 (不需判斷PropertyName)
            //PropertyChanged += (s, e) => {
            //    if (e.PropertyName == nameof(IsErrorMode)) OnPropertyChanged(nameof(CurrentLogs)); };
        }
        private async Task SaveLogsAsync()
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
                _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: SaveLogs");
                _log.AddErrorLog($"SaveLogs Aggre.Ex: {ex.ToString()}");
            }
            catch (Exception ex)
            {
                // 最外層保護，抓所有未預期的錯誤
                ProgressString = Properties.Resources.MainProgressStopped;
                _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: SaveLogs");
                _log.AddErrorLog($"SaveLogs Ex: {ex.ToString()}");
            }
        }

        // 測試
        private async Task SqlTestFunc()
        {
            try
            {
                Debug.WriteLine($"連線狀態: {_dataService.EquipmentRep.CheckConnection()}");
                await _dataService.Demo();
            }
            catch (SqlException sqlex)
            {
                Debug.WriteLine($"SqlException: {sqlex.Message}");
            }
            catch (TaskCanceledException taskex)
            {
                Debug.WriteLine($"TaskCanceledException: {taskex.Message}");
            }
            catch(AggregateException aggEx) 
            {
                Debug.WriteLine($"TaskCanceledException: {aggEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}");
            }
        }

    }

}
