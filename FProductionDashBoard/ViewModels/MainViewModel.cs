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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FProductionDashBoard.ViewModels
{
    public enum NavMode { Home, Operation, View, List, Equipment, Order }
    public partial class MainViewModel : ObservableObject
    {
        public string AppVersion { get; }
        public DispatcherTimer DefaultTimer;

        public ObservableCollection<object> Cards { get; set; } = new();
        [ObservableProperty]
        public object? mainCard; // 主視窗中的主卡片
        private DeviceCardContainerViewModel? _deviceContainer;

        #region  -- DI注入資源 --
        private readonly DashboardCoreServices _core;
        private readonly IOfflineSyncService _syncService;
        private readonly MultiCardReaderService _multiCardReaderService;
        private readonly Services.WebApi.IErpApiService _erpApiService;
        public LogService _log => _core.Log; // public 是為了Window的顯示
        public UserInfo? SystemUser => _core.Authorization.CurrentUser;
        public UserInfo? CurrentUser => _core.Authorization.CurrentUser;
        public bool IsLoggedIn => _core.Authorization.IsLoggedIn;
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
        public bool isNetConnected = false; // DB / WEBAPI 連線狀態 (由 Reload CommonList 檢查連線)
        [ObservableProperty]
        public string netStatusTooltip = ""; // 連線狀態訊息
        #endregion

        public ObservableCollection<LogEntry> CurrentLogs => IsErrorMode ? _core.Log.ErrorLogs : _core.Log.Logs;
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
        private int _logInOutCounter = 0;
        private int _isSyncing = 0;

        public MainViewModel(DashboardCoreServices core, IOfflineSyncService syncService,
            MultiCardReaderService multiCardReaderService,
            Services.WebApi.IErpApiService erpApiService)
        {
            // 讀取 FileVersion
            AppVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion ?? "Unknown";

            // DI注入 Repository
            _core = core;
            _syncService = syncService;
            _multiCardReaderService = multiCardReaderService;
            _erpApiService = erpApiService;
            _core.CardReader.CardRead += OnCardRead;

            // 定義工作起始時間 (於 DispatcherTimer 偵測更新)
            _core.Data.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);

            // 建立 DispatcherTimer 每秒更新一次時間，此方法位於 "UI 執行緒" 需確認是否移至 "執行緒池"
            DefaultTimer = new DispatcherTimer();
            DefaultTimer.Interval = TimeSpan.FromSeconds(1);
            DefaultTimer.Tick += async (s, e) => await OnTimerTickAsync();
            DefaultTimer.Start();

            InitializeCommand = new AsyncRelayCommand(LoadAllListsAsync);
            // 設定元件事件 (導覽列)
            CollapseNavCommand = new RelayCommand(() => { IsCollapsedNav = !IsCollapsedNav; });
            SwitchModeCommand = new RelayCommand<NavMode>(SwitchMode,
                (NavMode) => _core.Authorization.HasPermission(Services.PermissionId.View));

            //  設定元件事件 (工具列)
            TestCommand = new AsyncRelayCommand(() => SqlTestFunc(),
                () => _core.Authorization.HasPermission(Services.PermissionId.Test));

            // 設定元件事件 (帳號) 
            LoginCommand = new AsyncRelayCommand(LoginAsync);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync);

            // 設定元件事件 (訊息視窗)
            SaveLogsCommand = new AsyncRelayCommand(() => SaveLogsAsync());

            // (訂閱端) 使用者變更事件，刷新命令狀態與使用者顯示 (若 _authService 在執行緒池)
            _core.Authorization.UserChanged += () => System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!_core.Authorization.IsLoggedIn) // 若登出則執行對應事件
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

        #region -- 載入初始化 與 計時器 --
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

                CommonLists = new ListsFromSql
                {
                    DevicesList  = t1.Result,
                    UsersList    = t2.Result,
                    MaterialsList = t3.Result,
                    ErrorsList   = t4.Result,
                    TimeSlotsList = t5.Result,
                    RolesList    = t6.Result
                };
                _core.Log.AddLog($"已載入清單: " +
                    $"Devices:[{CommonLists.DevicesList.Count}]-" +
                    $"Users:[{CommonLists.UsersList.Count}]-" +
                    $"Materials:[{CommonLists.MaterialsList.Count}]-" +
                    $"Errors:[{CommonLists.ErrorsList.Count}]-" +
                    $"TimeSlots:[{CommonLists.TimeSlotsList.Count}]-" +
                    $"Roles:[{CommonLists.RolesList.Count}]");
                //await SyncAndLogAsync();
                //await CheckMissedInspectionsAsync();
            }
            finally
            {
                loadingWin.Close();
            }
        }
        private async Task<List<T>> FetchListAsync<T>(Func<Task<List<T>>> fetch)
        {
            try { return await fetch(); }
            catch (Exception ex)
            {
                IsNetConnected = false;
                _core.Log.AddLog($"{Properties.Resources.ComStrErrorTitle}: {ex.Message}", LogLevel.Error);
                return [];
            }
        }

        // Timer Tick 自動偵測邏輯
        private async Task OnTimerTickAsync()
        {
            CurrentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            if (DateTime.Now.AddDays(-1) > _core.Data.BusinessDay)
                _core.Data.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);

            // 狀態列 狀態更新
            OnPropertyChanged(nameof(IsCardReaderConnected));
            OnPropertyChanged(nameof(CardReaderStatusTooltip));

            _syncTickCounter++;
            if (_syncTickCounter >= 60)
            {
                _syncTickCounter = 0;
                _ = Task.Run(SyncAndLogAsync);
            }

            _missedCheckCounter++;
            if (_missedCheckCounter >= 300)
            {
                _missedCheckCounter = 0;
                _ = Task.Run(CheckMissedInspectionsAsync);
            }

            _logInOutCounter++;
            if (_logInOutCounter >= 600)
            {
                _logInOutCounter = 0;
                await CheckLogOutForLongIdle();
            }
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
            finally
            { Interlocked.Exchange(ref _isSyncing, 0); }
        }
        // 未巡檢偵測與插入 -- 巡檢狀態更新 (5分鐘檢查) (若離線狀態延至下個工作日，則前日未插入之資料將會遺漏) ** 
        private async Task CheckMissedInspectionsAsync()
        {
            var activeDevices = _deviceContainer?.Devices;
            if (activeDevices == null || !activeDevices.Any()) return;
            if (CommonLists.TimeSlotsList.Count == 0) return;

            var deviceSnapshot = activeDevices.ToList();
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
                    _core.Log.AddLog($"逾時補填失敗 [{card.Info.Name}]: {ex.Message}", LogLevel.Error);
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
            vm.SessionExtended += (_, _) => {
                isExtendLogin = true;
                _core.Log.AddLog("已延長，歡迎回來", LogLevel.Success);
            };
            var win = new LoadingWindow(vm);
            vm.StartCountdown();
            win.ShowDialog();

            if (!isExtendLogin)
                await _core.Authorization.LogoutAsync();
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
            var loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() != true) { return; }

            _logInOutCounter = 0;
            await _core.Authorization.InitializeAsync(loginWindow.User);
        }
        private async Task LogoutAsync()
        {
            await _core.Authorization.LogoutAsync();
            _core.CardReader.ResetLastCard();
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
                    _deviceContainer ??= new DeviceCardContainerViewModel(_core, CurrentUser!, CommonLists);
                    MainCard = _deviceContainer;
                    break;

                case NavMode.View:
                    break;

                case NavMode.List:
                    MainCard = new SettingViewModel(_core);
                    break;

                case NavMode.Equipment:
                    MainCard = new HardwareViewModel(_multiCardReaderService);
                    break;

                case NavMode.Order:
                    break;

                default:
                    break;
            }
        }

        // 訊息窗事件
        partial void OnIsErrorModeChanged(bool value)
        {
            if (value && _core.Log.IsNewErrorLog) _core.Log.IsNewErrorLog = false;

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

                await _core.Log.SaveAllLogsToFileAsync();

                ProgressString = Properties.Resources.MainProgressSuccess;
            }
            catch (AggregateException ex)
            {
                ProgressString = Properties.Resources.MainProgressStopped;
                _core.Log.AddLog($"{Properties.Resources.ComStrErrorTitle}: SaveLogs");
                _core.Log.AddErrorLog($"SaveLogs Aggre.Ex: {ex.ToString()}");
            }
            catch (Exception ex)
            {
                // 最外層保護，抓所有未預期的錯誤
                ProgressString = Properties.Resources.MainProgressStopped;
                _core.Log.AddLog($"{Properties.Resources.ComStrErrorTitle}: SaveLogs");
                _core.Log.AddErrorLog($"SaveLogs Ex: {ex.ToString()}");
            }
        }

        // 讀卡機事件：記錄 log + 自動登入/換手 / ERP 查詢新增或更新卡號
        private async void OnCardRead(object? sender, Services.CardReadEventArgs e)
        {
            try
            {
                //_core.Log.AddLog($"[CardReader:{e.PortName}] {e.CardId}");
                var snapshot = CommonLists.UsersList.ToList();
                var user = snapshot.FirstOrDefault(u => u.CardId == e.CardId);

                if (user != null)
                {
                    if (_core.Authorization.CurrentUser?.CardId == e.CardId) return;
                    await _core.Authorization.InitializeAsync(user);
                    _core.Log.AddLog($"[CardReader] 登入: {user.Name}", LogLevel.Success);
                    return;
                }

                // 未在 UsersList 找到 → 查詢 ERP
                var emp = await _erpApiService.GetEmpInfoByCardAsync(e.CardId);
                if (emp == null)
                {
                    _core.Log.AddLog($"[CardReader] 未識別卡號: {e.CardId}", LogLevel.Warning);
                    return;
                }

                var existingUser = snapshot.FirstOrDefault(u => u.UserId == emp.EmpNo);
                if (existingUser == null)
                    await HandleAddNewEmployeeAsync(emp, e.CardId);
                else
                    await HandleUpdateCardIdAsync(existingUser, e.CardId);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog($"[CardReader] 處理失敗: {ex.Message}", LogLevel.Error);
            }
        }

        // 依賴 ERP 查詢新增或更新卡號
        private async Task HandleAddNewEmployeeAsync(Dtos.EmpInfoDto emp, string cardId)
        {
            var msg = $"查詢到 ERP 員工資訊\n\n員工編號：{emp.EmpNo}\n姓名：{emp.Name}\n卡號：{cardId}\n\n" +
                $"是否新增至系統？\n（預設訪客權限，密碼 0000）";

            bool confirmed = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var vm = new DialogBaseViewModel<object>(msg);
                new DialogWindow(vm).ShowDialog();
                return vm.IsConfirmed;
            });

            if (!confirmed) { _core.CardReader.ResetLastCard(); return; }

            var dto = new Dtos.EmployeeFormDto
            {
                UserId = emp.EmpNo,
                Name = emp.Name,
                CardId = cardId,
                Password = "0000",
                RoleId = 1
            };
            await _core.Data.AddEmployeeAsync(dto);
            var newList = await _core.Data.GetUsersAsync();
            await Application.Current.Dispatcher.InvokeAsync(() => CommonLists.UsersList = newList);
            _core.Log.AddLog($"[CardReader] 已新增員工: {emp.Name}，請重新刷卡登入", LogLevel.Success);
            _core.CardReader.ResetLastCard();
        }
        private async Task HandleUpdateCardIdAsync(UiModels.UserInfo existingUser, string cardId)
        {
            var msg = $"系統已有此員工\n\n員工編號：{existingUser.UserId}\n姓名：{existingUser.Name}\n\n新卡號：{cardId}\n\n" +
                $"是否更新卡號至資料庫？";

            bool confirmed = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var vm = new DialogBaseViewModel<object>(msg);
                new DialogWindow(vm).ShowDialog();
                return vm.IsConfirmed;
            });

            if (!confirmed) { _core.CardReader.ResetLastCard();return; }

            var dto = new Dtos.EmployeeFormDto
            {
                Id = existingUser.Id,
                UserId = existingUser.UserId,
                Name = existingUser.Name,
                CardId = cardId,
                Password = "",
                RoleId = existingUser.RoleId,
                Email = existingUser.Email,
                DepartmentId = existingUser.DepartmentId
            };
            await _core.Data.UpdateEmployeeAsync(dto);
            await Application.Current.Dispatcher.InvokeAsync(() => existingUser.CardId = cardId);
            _core.Log.AddLog($"[CardReader] 更新卡號: {existingUser.Name}，請重新刷卡登入", LogLevel.Success);
            _core.CardReader.ResetLastCard();
        }

        // 測試
        private async Task SqlTestFunc()
        {
            try
            {
                Debug.WriteLine($"連線狀態: {_core.Data.EquipmentRep.CheckConnection()}");
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

            //try
            //{
            //    await _dataService.AddTeachingRecordAsync(1, 1, 600, "AAA");
            //    //await AddOffsetRecordAsync(1, 1, 60, "BBB");
            //}
            //catch (OfflineOperationQueuedException)
            //{
            //    _log.AddLog("帶點紀錄已暫存，待連線恢復後自動上傳", LogLevel.Warning);
            //}
            //catch (Exception ex)
            //{
            //    _log.AddLog("帶點紀錄上傳異常");
            //    _log.AddErrorLog($"Tuning Record: {ex.Message}");
            //}
        }

    }

}
