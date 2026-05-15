using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.Exceptions;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.UiModels;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace FProductionDashBoard.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        public string AppVersion => typeof(App).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        public DispatcherTimer DefaultTimer;

        public ObservableCollection<object> Cards { get; set; } = new();
        [ObservableProperty]
        public object? mainCard;
        [ObservableProperty]
        private LayoutMode currentLayout = LayoutMode.Single;
        private DeviceCardContainerViewModel? _deviceContainer;
        private readonly Dictionary<PanelViewModel, DeviceCardContainerViewModel> _panelContainers = new();

        public PanelViewModel Panel1 { get; }
        public PanelViewModel Panel2 { get; }
        public PanelViewModel Panel3 { get; }
        public PanelViewModel Panel4 { get; }
        private PanelViewModel[] _panels = [];
        public IRelayCommand<LayoutMode> SetLayoutCommand { get; }

        #region  -- DI注入資源 --
        private readonly DashboardCoreServices _core;
        private readonly IOfflineSyncService _syncService;
        private readonly MultiCardReaderService _multiCardReaderService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialog;
        private readonly CardReaderHandler _cardReaderHandler;

        #endregion

        [ObservableProperty]
        public string? systemUser;
        public UserInfo? CurrentUser => _core.Authorization.CurrentUser;
        public bool IsLoggedIn => _core.Authorization.IsLoggedIn;
        
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
        private bool isLogPanelVisible = true; // 訊息面板顯示
        [ObservableProperty]
        private int progressValue = 0; // 進度調數值
        [ObservableProperty]
        private string progressString = Properties.Resources.MainProgressIdle; // 進度條說明
        [ObservableProperty]
        private bool isProgressIndeterminate = false; // 進度條循環
        [ObservableProperty]
        private bool isProgressVisible = false; // 進度條顯示
        [ObservableProperty]
        private NavMode currentNavMode = NavMode.Home; // 當前導覽列模式
        public bool IsCardReaderConnected => _multiCardReaderService.IsConnected; //讀卡機連線狀態
        public string CardReaderStatusTooltip => BuildCardReaderTooltip(); //讀卡機訊息
        [ObservableProperty]
        private bool isNetConnected = false; // DB / WEBAPI 連線狀態 (由 Reload CommonList 檢查連線)
        [ObservableProperty]
        private string netStatusTooltip = ""; // 連線狀態訊息
        #endregion

        public ListsFromSql CommonLists = new();

        public ICommand InitializeCommand { get; }
        // 菜單列

        // 狀態列
        public IAsyncRelayCommand LoginCommand { get; } // 登入事件
        public IAsyncRelayCommand LogoutCommand { get; } // 登出事件

        // 工具列
#if DEBUG
        public IRelayCommand TestCommand { get; }
#endif
        // 導覽列
        public ICommand CollapseNavCommand { get; }
        public IRelayCommand SwitchModeCommand { get; }
        // 訊息窗
        public LogPanelViewModel LogPanel { get; }
        // 外觀設定（供 Menu 直接切換語言/主題）
        public SystemSettingsViewModel SystemSettings { get; }
        // 主視覺視窗

        private int _syncTickCounter = 0;
        private int _missedCheckCounter = 0;
        private int _isSyncing = 0;

        // 閒置更新機制
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        private int GetIdleSeconds()
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            GetLastInputInfo(ref info);
            return (int)((uint)Environment.TickCount - info.dwTime) / 1000;
        }

        private Action? _onUserChanged; // 可被取消註冊

        public MainViewModel(DashboardCoreServices core, IOfflineSyncService syncService,
            MultiCardReaderService multiCardReaderService,
            IServiceProvider sp,
            IDialogService dialogService)
        {
            // DI注入 Repository
            _core = core;
            _syncService = syncService;
            _multiCardReaderService = multiCardReaderService;
            _serviceProvider = sp;
            _dialog = dialogService;
            _cardReaderHandler = new CardReaderHandler(
                _core, sp.GetRequiredService<Services.WebApi.IErpApiService>(), _dialog, CommonLists);
            _cardReaderHandler.Attach();

            // 定義工作起始時間 (於 DispatcherTimer 偵測更新)
            BusinessHour = Properties.Settings.Default.BusinessHour;
            BusinessMinute = Properties.Settings.Default.BusinessMinute;
            _core.Data.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);

            // 建立 DispatcherTimer 每秒更新一次時間，此方法位於 "UI 執行緒" 需確認是否移至 "執行緒池"
            DefaultTimer = new DispatcherTimer();
            DefaultTimer.Interval = TimeSpan.FromSeconds(1);
            DefaultTimer.Tick += OnTimerTick;
            DefaultTimer.Start();

            InitializeCommand = new AsyncRelayCommand(LoadAllListsAsync);
            // 設定元件事件 (導覽列)
            CollapseNavCommand = new RelayCommand(() => { IsCollapsedNav = !IsCollapsedNav; });
            SwitchModeCommand = new RelayCommand<NavMode>(SwitchMode,
                (_) => _core.Authorization.HasPermission(PermissionId.View));

            //  設定元件事件 (工具列)

#if DEBUG // 測試函式用
            TestCommand = new AsyncRelayCommand(() => SqlTestFunc(),
                () => _core.Authorization.HasPermission(PermissionId.Test));
#endif
            // 設定元件事件 (帳號)
            LoginCommand = new AsyncRelayCommand(LoginAsync);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync);

            // 訊息面板 ViewModel
            LogPanel = sp.GetRequiredService<LogPanelViewModel>();

            // 外觀設定 ViewModel（供 Menu 直接切換語言/主題）
            SystemSettings = sp.GetRequiredService<SystemSettingsViewModel>();
            SystemSettings.LoadFromSettings();

            // 版面配置 Panels
            Panel1 = new PanelViewModel(SwitchPanelContent);
            Panel2 = new PanelViewModel(SwitchPanelContent);
            Panel3 = new PanelViewModel(SwitchPanelContent);
            Panel4 = new PanelViewModel(SwitchPanelContent);
            _panels = [Panel1, Panel2, Panel3, Panel4];
            SetLayoutCommand = new RelayCommand<LayoutMode>(SetLayout);
            foreach (var ipandel in _panels)
                SwitchPanelContent(ipandel, NavMode.Home);

            // 初始登入人員
            SystemUser = $"{Properties.Resources.ComStrSystemUser}: {_core.Authorization.CurrentUser!.Name}";

            _onUserChanged = () => OnUserChanged();
            _core.Authorization.UserChanged += _onUserChanged;
        }

        #region -- 載入初始化 與 計時器 --
        public async Task LoadAllListsAsync()
        {
            // 新增載入視窗 (評估此視窗的開啟位置 以及 是否 disable 主視窗)
            var loadingVm = new LoadingViewModel
            {
                Mode = LoadingMode.Processing,
                Message = Properties.Resources.LoadingInitMessage,
                CanCancel = false
            };
            var loadingWin = new LoadingWindow(loadingVm); //Application.Current.MainWindow.IsEnabled = false;
            loadingWin.Show();

            try
            {
                IsNetConnected = true;
                var sb = new StringBuilder("最後更新時間:\n");
                NetStatusTooltip = sb.Append(DateTime.Now.ToString("yyyy/MM/dd HH:mm")).ToString().TrimEnd();

                var t1 = FetchListAsync(() => _core.Data.GetDevicesAsync());
                var t2 = FetchListAsync(() => _core.Data.GetUsersAsync());
                var t3 = FetchListAsync(() => _core.Data.GetMaterialsAsync());
                var t4 = FetchListAsync(() => _core.Data.GetErrorsAsync(Properties.Settings.Default.CultureCode));
                var t5 = FetchListAsync(() => _core.Data.GetTimeSlotsAsync());
                var t6 = FetchListAsync(() => _core.Data.GetAllRolesAsync());
                await Task.WhenAll(t1, t2, t3, t4, t5, t6);

                CommonLists.DevicesList   = t1.Result;
                CommonLists.UsersList     = t2.Result;
                CommonLists.MaterialsList = t3.Result;
                CommonLists.ErrorsList    = t4.Result;
                CommonLists.TimeSlotsList = t5.Result;
                CommonLists.RolesList     = t6.Result;
                if (CommonLists.RolesList.Any())
                    _core.Authorization.SetCachedRoles(CommonLists.RolesList);

                _core.Log.AddLog($"已載入清單: " +
                    $"Devices:[{CommonLists.DevicesList.Count}]-" +
                    $"Users:[{CommonLists.UsersList.Count}]-" +
                    $"Materials:[{CommonLists.MaterialsList.Count}]-" +
                    $"Errors:[{CommonLists.ErrorsList.Count}]-" +
                    $"TimeSlots:[{CommonLists.TimeSlotsList.Count}]-" +
                    $"Roles:[{CommonLists.RolesList.Count}]");
            }
            finally { loadingWin.Close(); }
        }
        private async Task<List<T>> FetchListAsync<T>(Func<Task<List<T>>> fetch)
        {
            try { return await fetch(); }
            catch (Exception ex)
            {
                IsNetConnected = false;
                _core.Log.AddLog("清單載入失敗，請確認連線", LogLevel.Error);
                _core.Log.AddErrorLog($"[FetchListAsync] {ex.Message}");
                return [];
            }
        }
        // 人員切換權限觸發事件
        private void OnUserChanged()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 依 Policy 清除管理類 panel（監控類如 Operation 保留）
                    foreach (var p in _panels)
                    {
                        if (NavModeDescriptor.Of(p.CurrentNavMode).ClearOnUserChange)
                        {
                            p.Content = null;
                            p.SetMode(NavMode.Home);
                        }
                    }
                    MainCard = Panel1.Content;

                    if (!_core.Authorization.IsLoggedIn)
                    {
                        // _deviceContainer = null;
                        // _panelContainers.Clear();
                    }

                    SwitchModeCommand.NotifyCanExecuteChanged();
                    LogoutCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(CurrentUser));
                    OnPropertyChanged(nameof(IsLoggedIn));

#if DEBUG // 測試函式用
                    TestCommand.NotifyCanExecuteChanged();
#endif
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog("使用者切換時發生錯誤", LogLevel.Error);
                    _core.Log.AddErrorLog($"[OnUserChanged] {ex.Message}");
                }
            });
        }
        // Timer Tick 自動偵測邏輯
        private async void OnTimerTick(object? s, EventArgs e) => await OnTimerTickAsync();
        private async Task OnTimerTickAsync()
        {
            CurrentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            if (DateTime.Now.AddDays(-1) > _core.Data.BusinessDay)
                _core.Data.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);

            // 狀態列 狀態更新
            OnPropertyChanged(nameof(IsCardReaderConnected));
            OnPropertyChanged(nameof(CardReaderStatusTooltip));

            var s = Properties.Settings.Default;

            _syncTickCounter++;
            if (s.SyncEnabled && _syncTickCounter >= s.SyncIntervalSec)
            {
                _syncTickCounter = 0;
                _ = Task.Run(async ()=>
                {
                    try { await SyncAndLogAsync(); }
                    catch (Exception ex)
                    {
                        _core.Log.AddLog("[SyncAndLogAsync] 同步背景任務發生例外", LogLevel.Error);
                        _core.Log.AddErrorLog($"[SyncAndLogAsync] {ex.Message}");
                    }
                });
            }

            _missedCheckCounter++;
            if (s.MissedCheckEnabled && _missedCheckCounter >= s.MissedCheckIntervalSec)
            {
                _missedCheckCounter = 0;
                var containerSnapshot = new[] { _deviceContainer }
                    .Concat(_panelContainers.Values)
                    .Where(c => c != null)
                    .Cast<DeviceCardContainerViewModel>()
                    .ToList();
                _ = Task.Run(async () =>
                {
                    try { await CheckMissedInspectionsAsync(containerSnapshot); }
                    catch (Exception ex)
                    {
                        _core.Log.AddLog("[CheckMissedInspectionsAsync] 補填背景任務發生例外", LogLevel.Error);
                        _core.Log.AddErrorLog($"[CheckMissedInspectionsAsync] {ex.Message}");
                    }
                });
            }

            if (s.IdleLogoutEnabled && IsLoggedIn && GetIdleSeconds() >= s.IdleLogoutIntervalSec)
                await CheckLogOutForLongIdle();
        }
        // 同步暫存資料  (1分鐘檢查)
        private async Task SyncAndLogAsync()
        {
            if (Interlocked.CompareExchange(ref _isSyncing, 1, 0) != 0) return;
            try
            {
                var result = await _syncService.SyncPendingAsync();
                // TODO: 根據 result.SyncedCount / result.FailedCount 決定 log 輸出時機
                if (result?.SyncedCount > 0)
                    _core.Log.AddLog($"已重新連線: 上傳{result?.SyncedCount}筆暫存資料");
                if (result?.FailedCount > 0) // 無效，因為離線永遠回傳0
                    _core.Log.AddLog($"連線失敗: {result?.FailedCount}筆資料等待上傳", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("同步暫存資料失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[SyncAndLogAsync] {ex.Message}");
            }
            finally
            { Interlocked.Exchange(ref _isSyncing, 0); }
        }
        // 未巡檢偵測與插入 -- 巡檢狀態更新 (5分鐘檢查) (若離線狀態延至下個工作日，則前日未插入之資料將會遺漏) **
        private async Task CheckMissedInspectionsAsync(
            IReadOnlyList<DeviceCardContainerViewModel> containers)
        {
            if (CommonLists.TimeSlotsList.Count == 0) return;
            var activeDevices = containers
                .SelectMany(c => c.Devices)
                .Distinct()
                .ToList();
            if (!activeDevices.Any()) return;

            var deviceSnapshot = activeDevices;
            foreach (var card in deviceSnapshot)
            {
                try
                {
                    await _core.Data.CheckAndInsertMissedInspectionAsync(
                        CommonLists.TimeSlotsList, card.Info.Id);

                    await card.UpdateTimeSlotsStatusAsync(); // 更新每台設備的巡檢狀態
                }
                catch (InvalidOperationException)
                {
                    _core.Log.AddLog("逾時補填/狀態更新略過：資料庫連線失敗", LogLevel.Warning);
                    break;
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"逾時補填失敗 [{card.Info.Name}]", LogLevel.Error);
                    _core.Log.AddErrorLog($"[CheckMissedInspectionsAsync] [{card.Info.Name}] {ex.Message}");
                }
            }
        }
        // 閒置過久 -- 逾時登出 (目前為10分鐘詢問一次，並未真正從閒置開始計時，待優化) **
        private async Task CheckLogOutForLongIdle()
        {
            if (!IsLoggedIn) return;

            bool isExtendLogin = false;
            var vm = new LoadingViewModel
            {
                Mode = LoadingMode.LogoutCountdown,
                Message = "閒置逾時警告",
                CountdownSeconds = 10
            };
            vm.SessionExtended += (_, _) =>
            {
                isExtendLogin = true;
                _core.Log.AddLog("已延長，歡迎回來", LogLevel.Success);
            };
            var win = new LoadingWindow(vm);
            vm.StartCountdown();
            win.ShowDialog();

            if (!isExtendLogin)
                await _core.Authorization.LogoutAsync();
        }

        public void Dispose()
        {
            DefaultTimer.Stop();
            DefaultTimer.Tick -= OnTimerTick;
            _cardReaderHandler.Detach();
            if (_onUserChanged != null)
                _core.Authorization.UserChanged -= _onUserChanged;
        }

        #endregion

        // 工具列 與 狀態列 事件
        private string BuildCardReaderTooltip()
        {
            var sb = new StringBuilder("讀卡機狀態\n");
            if (!_multiCardReaderService.Readers.Any())
                return sb.Append("（未設定讀卡機）").ToString();
            foreach (var r in _multiCardReaderService.Readers)
                sb.AppendLine($"{(r.IsConnected ? "●" : "○")} {r.PortName}  {r.BaudRate}  {(r.IsConnected ? "已連線" : "離線")}");
            return sb.ToString().TrimEnd();
        }
        private async Task LoginAsync()
        {
            var loginWindow = new LoginWindow(isSettingsEnabled: false);
            if (loginWindow.ShowDialog() != true) { return; }

            try
            {
                await _core.Authorization.InitializeAsync(loginWindow.User);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("登入失敗，請稍後重試", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoginAsync] {ex.Message}");
            }
        }
        private async Task LogoutAsync()
        {
            try
            {
                await _core.Authorization.LogoutAsync();
                _core.CardReader.ResetLastCard();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("登出失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[LogoutAsync] {ex.Message}");
            }
        }
        private void SetProgress(string message, bool visible = true, bool indeterminate = false, int value = 0)
        {
            ProgressString = message;
            IsProgressVisible = visible;
            IsProgressIndeterminate = indeterminate;
            ProgressValue = value;
        }
        private void ClearProgress() => SetProgress(Properties.Resources.MainProgressIdle, visible: false);
        // 導覽列事件（永遠切換 Panel1，向下相容）
        public void SwitchMode(NavMode mode)
        {
            if (mode.Equals(CurrentNavMode)) return;
            bool success = SwitchPanelContent(Panel1, mode);
            if (success) CurrentNavMode = mode;
        }

        private bool SwitchPanelContent(PanelViewModel panel, NavMode mode)
        {
            object? content = null;
            switch (mode)
            {
                case NavMode.Home:
                    content = _serviceProvider.GetRequiredService<HomeViewModel>();
                    break;

                case NavMode.Operation:
                    if (!_core.Authorization.HasAnyPermission(
                        PermissionId.OperateInspection, PermissionId.OperateMaterial,
                        PermissionId.OperateTuning, PermissionId.Order)) return false;
                    content = GetOrCreateContainer(panel);
                    break;

                case NavMode.List:
                    if (!_core.Authorization.HasPermission(PermissionId.Edit)) return false;
                    content = _serviceProvider.GetRequiredService<SettingViewModel>();
                    break;

                case NavMode.Equipment:
                    if (!_core.Authorization.HasPermission(PermissionId.Setting)) return false;
                    content = _serviceProvider.GetRequiredService<HardwareViewModel>();
                    break;

                case NavMode.Order:
                    if (!_core.Authorization.HasAnyPermission(PermissionId.Order, PermissionId.Schedule)) return false;
                    break;

                case NavMode.SystemSettings:
                    if (!_core.Authorization.HasPermission(PermissionId.Setting)) return false;
                    var ssVm = _serviceProvider.GetRequiredService<SystemSettingsViewModel>();
                    ssVm.LoadFromSettings();
                    content = ssVm;
                    break;
            }

            // 排他性：singleton mode 若其他 panel 已顯示，將其清空
            var policy = NavModeDescriptor.Of(mode);
            if (!policy.IsRepeatable)
            {
                var occupied = _panels.FirstOrDefault(p => p != panel && p.CurrentNavMode == mode);
                if (occupied != null)
                    SwitchPanelContent(occupied, NavMode.Home);
            }

            panel.SetMode(mode);
            panel.Content = content;
            if (panel == Panel1) MainCard = content;
            return true;
        }

        private DeviceCardContainerViewModel GetOrCreateContainer(PanelViewModel panel)
        {
            if (panel == Panel1)
            {
                _deviceContainer ??= new DeviceCardContainerViewModel(_core, _dialog, CommonLists);
                return _deviceContainer;
            }
            if (!_panelContainers.TryGetValue(panel, out var vm))
                _panelContainers[panel] = vm = new DeviceCardContainerViewModel(_core, _dialog, CommonLists);
            return vm;
        }

        private void SetLayout(LayoutMode layout)
        {
            CurrentLayout = layout;
            bool isMulti = layout != LayoutMode.Single;
            foreach (var p in _panels) p.ShowHeader = isMulti;
        }


#if DEBUG // 測試函式用
        private async Task SqlTestFunc()
        {
            try
            {
                await _core.Data.Demo();
            }
            catch (SqlException sqlex)
            {
                Debug.WriteLine($"SqlException: {sqlex.Message}");
            }
            catch (TaskCanceledException taskex)
            {
                Debug.WriteLine($"TaskCanceledException: {taskex.Message}");
            }
            catch (AggregateException aggEx)
            {
                Debug.WriteLine($"TaskCanceledException: {aggEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}");
            }
        }
#endif

    }
}
