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
        private readonly IOfflineSyncService _syncService;
        private readonly MultiCardReaderService _multiCardReaderService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialog;
        private readonly CardReaderHandler _cardReaderHandler;
        #endregion

        [ObservableProperty] public string? systemUser;
        public UserInfo? CurrentUser => _core.Authorization.CurrentUser;
        public bool IsLoggedIn => _core.Authorization.IsLoggedIn;

        #region -- 介面邏輯 --
        [ObservableProperty] private bool isCollapsedNav = false;
        [ObservableProperty] private bool isLogPanelVisible = true;
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
#if DEBUG
        public IRelayCommand TestCommand { get; }
#endif
        public ICommand CollapseNavCommand { get; }
        public LogPanelViewModel LogPanel { get; }
        public SystemSettingsViewModel SystemSettings { get; }

        private Action? _onUserChanged;

        partial void InitializeScheduler();
        partial void InitializePanelLayout();
        partial void DisposeScheduler();

        public MainViewModel(DashboardCoreServices core, IOfflineSyncService syncService,
            MultiCardReaderService multiCardReaderService,
            IServiceProvider sp,
            IDialogService dialogService,
            ListsFromSql commonLists)
        {
            _core = core;
            _syncService = syncService;
            _multiCardReaderService = multiCardReaderService;
            _serviceProvider = sp;
            _dialog = dialogService;
            CommonLists = commonLists;
            _cardReaderHandler = new CardReaderHandler(
                _core, sp.GetRequiredService<Services.WebApi.IErpApiService>(), _dialog, CommonLists);
            _cardReaderHandler.Attach();

            InitializeScheduler();

            InitializeCommand = new AsyncRelayCommand(LoadAllListsAsync);
            CollapseNavCommand = new RelayCommand(() => { IsCollapsedNav = !IsCollapsedNav; });

#if DEBUG
            TestCommand = new AsyncRelayCommand(() => SqlTestFunc(),
                () => _core.Authorization.HasPermission(PermissionId.Test));
#endif

            LoginCommand = new AsyncRelayCommand(LoginAsync);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync);

            LogPanel = sp.GetRequiredService<LogPanelViewModel>();
            SystemSettings = sp.GetRequiredService<SystemSettingsViewModel>();
            SystemSettings.LoadFromSettings();

            InitializePanelLayout();

            SystemUser = $"{Properties.Resources.ComStrSystemUser}: {_core.Authorization.CurrentUser!.Name}";
            _onUserChanged = () => OnUserChanged();
            _core.Authorization.UserChanged += _onUserChanged;
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

#if DEBUG
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
        #endregion

        public void Dispose()
        {
            DisposeScheduler();
            _cardReaderHandler.Detach();
            if (_onUserChanged != null)
                _core.Authorization.UserChanged -= _onUserChanged;
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

#if DEBUG
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
