using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.Exceptions;
using FProductionDashBoard.UiModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        public string AppVersion => typeof(App).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        #region -- DI注入資源 --
        private readonly DashboardCoreServices _core;
        private readonly MultiCardReaderService _multiCardReaderService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialog;
        private readonly CardReaderHandler _cardReaderHandler;
        private readonly IConfigService<SystemConfigDto> _systemConfig;
        private readonly ILicenseService _licenseService;
        private readonly ConnectionStatusService _connectionStatus;
        #endregion

        [ObservableProperty] public string? systemUser;
        public UserInfo? CurrentUser => _core.Authorization.CurrentUser;
        public bool IsLoggedIn => _core.Authorization.IsLoggedIn;

        #region -- 介面邏輯 --
        [ObservableProperty] private bool isCollapsedNav = false;
        [ObservableProperty] private bool isLogPanelVisible = true;
        [ObservableProperty] private bool isAiAgentVisible = false;
        public bool HasAiAgentPermission => _core.Authorization.HasPermission(PermissionId.Test);
        [ObservableProperty] private int progressValue = 0;
        [ObservableProperty] private string progressString = Properties.Resources.MainProgressIdle;
        [ObservableProperty] private bool isProgressIndeterminate = false;
        [ObservableProperty] private bool isProgressVisible = false;
        public bool IsCardReaderConnected => _multiCardReaderService.IsConnected;
        public string CardReaderStatusTooltip => BuildCardReaderTooltip();
        [ObservableProperty] private bool isNetConnected = false;
        [ObservableProperty] private string netStatusTooltip = "";
        #endregion

        public ListsFromSql CommonLists;

        public ICommand InitializeCommand { get; }
        public IAsyncRelayCommand LoginCommand { get; }
        public IAsyncRelayCommand LogoutCommand { get; }
        public IRelayCommand TestCommand { get; }
        public ICommand CollapseNavCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public LogPanelViewModel LogPanel { get; }
        public AIAgentViewModel AiAgent { get; }
        public SystemSettingsViewModel SystemSettings { get; }

        // 即時通知浮層通道狀態（供 MainWindow 的 Snackbar 綁定）
        public SnackbarNotificationChannel Notifications { get; }

        private Action? _onUserChanged;
        private EventHandler? _onConnectionStatusChanged;

        partial void InitializeScheduler();
        partial void InitializePanelLayout();
        partial void DisposeScheduler();

        public MainViewModel(DashboardCoreServices core,
            MultiCardReaderService multiCardReaderService,
            IServiceProvider sp,
            IDialogService dialogService,
            ListsFromSql commonLists,
            IConfigService<SystemConfigDto> systemConfig,
            ILicenseService licenseService,
            ConnectionStatusService connectionStatus,
            SnackbarNotificationChannel notifications)
        {
            _core = core;
            _multiCardReaderService = multiCardReaderService;
            _serviceProvider = sp;
            _dialog = dialogService;
            CommonLists = commonLists;
            _systemConfig = systemConfig;
            _licenseService = licenseService;
            _connectionStatus = connectionStatus;
            Notifications = notifications;

            _cardReaderHandler = new CardReaderHandler(
                _core, sp.GetRequiredService<Services.WebApi.IErpApiService>(), _dialog, CommonLists);
            _cardReaderHandler.Attach();

            InitializeScheduler();

            InitializeCommand = new AsyncRelayCommand(LoadAllListsAsync);
            CollapseNavCommand = new RelayCommand(() => { IsCollapsedNav = !IsCollapsedNav; });

            LoginCommand = new AsyncRelayCommand(LoginAsync);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync);

            LogPanel = sp.GetRequiredService<LogPanelViewModel>();
            AiAgent = sp.GetRequiredService<AIAgentViewModel>();
            SystemSettings = sp.GetRequiredService<SystemSettingsViewModel>();
            SystemSettings.LoadFromSettings();

            InitializePanelLayout();

            SystemUser = $"{Properties.Resources.ComStrSystemUser}: {_core.Authorization.CurrentUser!.Name}";
            _onUserChanged = () => OnUserChanged();
            _core.Authorization.UserChanged += _onUserChanged;

            _onConnectionStatusChanged = (_, _) => OnConnectionStatusChanged();
            _connectionStatus.StatusChanged += _onConnectionStatusChanged;
            OnConnectionStatusChanged();

            // 關於 - 版本 / ABB SDK / AI API Key / 授權狀態
            ShowAboutCommand = new RelayCommand(() =>
            {
                var title    = Application.Current.TryFindResource("SsAboutHeader") as string ?? "About";
                var verLabel = Application.Current.TryFindResource("SsVersionLabel") as string ?? "Version";

                var abbStatus = DeviceDrivers.Abb.AbbRobotClient.IsAbbPcSdkAvailable()
                    ? Properties.Resources.AboutSdkInstalled
                    : Properties.Resources.AboutSdkNotInstalled;
                var aiStatus = !string.IsNullOrEmpty(_systemConfig.Current.AiApiKey)
                    ? Properties.Resources.AboutApiKeyConfigured
                    : Properties.Resources.AboutApiKeyNotConfigured;
                var licenseStatus = Properties.Resources.ResourceManager
                    .GetString($"LicenseStatus{_licenseService.Status}")
                    ?? _licenseService.Status.ToString();

                MessageBox.Show(
                    $"{verLabel}: {AppVersion}\n\nABB PC SDK: {abbStatus}\nAI API Key: {aiStatus}\nLicense: {licenseStatus}",
                    title, MessageBoxButton.OK, MessageBoxImage.Information);
            });

            // 測試
            TestCommand = new RelayCommand(() => {});
        }

        #region -- 載入初始化 --
        public async Task LoadAllListsAsync()
        {
            var loadingVm = new LoadingViewModel
            {
                Mode = LoadingMode.Processing,
                Message = Properties.Resources.LoadingInitMessage,
                CanCancel = false
            };
            var loadingWin = new LoadingWindow(loadingVm);
            loadingWin.Show();

            try
            {
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
                _core.Log.AddLog("清單載入失敗，請確認連線", LogLevel.Error);
                _core.Log.AddErrorLog($"[FetchListAsync] {ex.Message}");
                return [];
            }
        }

        private void OnUserChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
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
                    OnPropertyChanged(nameof(HasAiAgentPermission));
                    if (!HasAiAgentPermission && IsAiAgentVisible)
                        IsAiAgentVisible = false;
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog("使用者切換時發生錯誤", LogLevel.Error);
                    _core.Log.AddErrorLog($"[OnUserChanged] {ex.Message}");
                }
            });
        }

        private void OnConnectionStatusChanged()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var snapshot = _connectionStatus.GetSnapshot();
                    IsNetConnected = snapshot.IsConnected;
                    NetStatusTooltip = BuildNetStatusTooltip(snapshot.IsConnected, snapshot.LastChangedAt);
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog("連線狀態更新時發生錯誤", LogLevel.Error);
                    _core.Log.AddErrorLog($"[OnConnectionStatusChanged] {ex.Message}");
                }
            });
        }

        // 燈號語意＝主資料庫（MesDbContext）連線；Data/Info context 不納入訊號源（刻意設計）
        private static string BuildNetStatusTooltip(bool isConnected, DateTime? lastChangedAt)
        {
            var template = isConnected ? Properties.Resources.NetStatusConnected
                                       : Properties.Resources.NetStatusDisconnected;
            var since = lastChangedAt?.ToString("yyyy/MM/dd HH:mm") ?? "-";
            return string.Format(template, since);
        }
        #endregion

        public void Dispose()
        {
            DisposeScheduler();
            DisposePanelContainers();
            _cardReaderHandler.Detach();
            if (_onUserChanged != null)
                _core.Authorization.UserChanged -= _onUserChanged;
            if (_onConnectionStatusChanged != null)
                _connectionStatus.StatusChanged -= _onConnectionStatusChanged;
        }

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
    }
}
