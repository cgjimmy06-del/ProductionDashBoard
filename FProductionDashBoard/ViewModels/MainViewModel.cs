using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    public class ListsFormSql
    {
        public List<DeviceInfo> DevicesList = new(); // 設備清單
        public List<MaterialInfo> MaterialsList = new(); // 物料清單
        public List<ErrorInfo> ErrorsList = new(); // 異常項目清單
        public List<UserInfo> UsersList = new(); // 人員清單
        public List<TimeSlotLookup> TimeSlotsList = new(); // 人員清單
        //public List<DeviceInfo> OrdersList = new(); // 排單點檢清單

    }
    public enum NavMode { Home, Operation, Setting, View }
    public partial class MainViewModel : ObservableObject
    {
        public string AppVersion { get; }
        public DispatcherTimer DefaultTimer;

        public ObservableCollection<object> Cards { get; set; } = new();
        [ObservableProperty]
        public object? card1; // 須重構

        #region  -- DI注入資源 --
        private readonly IDataService _dataService;
        private LogService _log { get; }
        private AuthorizationService _authService { get; }
        public UserInfo SystemUser { get; }
        public UserInfo CurrentUser { get; }
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
        private int progressValue = 0; // 進度數值
        [ObservableProperty]
        private string progressString = Properties.Resources.MainProgressIdle; // 進度訊息
        [ObservableProperty]
        private NavMode currentNavMode = NavMode.Home; // 當前導覽列模式
        #endregion

        public ObservableCollection<LogEntry> CurrentLogs => IsErrorMode ? _log.ErrorLogs : _log.Logs;
        public ListsFormSql CommonLists = new();

        public ICommand InitializeCommand { get; }
        // 菜單列
        // 工具列
        public ICommand TestCommand { get; }
        // 導覽列
        public ICommand CollapseNavCommand { get; }
        public ICommand SwitchModeCommand { get; }
        // 訊息窗
        public ICommand SaveLogsCommand { get; }
        // 主視覺視窗

        public MainViewModel(UserInfo user, LogService log, IDataService dataservice, AuthorizationService auth)
        {
            // 讀取 FileVersion
            AppVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion ?? "Unknown";

            // DI注入 Repository
            SystemUser = user;
            CurrentUser = user;
            _log = log;
            _dataService = dataservice;
            _authService = auth;

            // 建立 DispatcherTimer 每秒更新一次時間，並定義工作起始時間
            _dataService.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);

            DefaultTimer = new DispatcherTimer();
            DefaultTimer.Interval = TimeSpan.FromSeconds(1);
            DefaultTimer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
                if (DateTime.Now.AddDays(-1) > _dataService.BusinessDay)
                    _dataService.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);
            };
            DefaultTimer.Start();

            InitializeCommand = new AsyncRelayCommand(LoadAllListsAsync);
            // 設定元件事件 (導覽列)
            CollapseNavCommand = new RelayCommand(() => { IsCollapsedNav = !IsCollapsedNav; });
            SwitchModeCommand = new RelayCommand<NavMode>(SwitchMode, 
                (NavMode) => _authService.HasPermission(Services.Permission.View));

            //  設定元件事件 (工具列)
            TestCommand = new AsyncRelayCommand(() => SqlTestFunc(),
                () => _authService.HasPermission(Services.Permission.Test));

            // 設定元件事件 (訊息視窗)
            SaveLogsCommand = new AsyncRelayCommand(() => SaveLogsAsync());

            // 新增儀表卡片區
            //Cards.Add(new DeviceCardContainerViewModel(_log, _dataService, CurrentUser));

        }

        // 載入初始化 待翻譯log 並加上errorlog
        public async Task LoadAllListsAsync()
        {
            var devicesTask = await GetDevicesListFromSqlAsync();
            var usersTask = await GetUsersListFromSqlAsync();
            var materialsTask = await GetMaterialsListFromSqlAsync();
            var errorsTask = await GetErrorsListFromSqlAsync(Properties.Settings.Default.CultureCode);
            var timeslotsTask = (await _dataService.TimeSlotLookupRep.GetAllAsync()).OrderBy(s => s.TimeSlotId);

            // await Task.WhenAll(devicesTask, materialsTask, errorsTask); .Result // 無法同時開啟dbcontext

            CommonLists = new ListsFormSql
            {
                DevicesList = devicesTask,
                MaterialsList = materialsTask,
                ErrorsList = errorsTask,
                UsersList = usersTask,
                TimeSlotsList = timeslotsTask.ToList()
            };
            _log.AddLog($"已載入清單: " +
                $"Devices:[{CommonLists.DevicesList.Count}]-" +
                $"Users:[{CommonLists.UsersList.Count}]-" +
                $"Materials:[{CommonLists.MaterialsList.Count}]-" +
                $"Errors:[{CommonLists.ErrorsList.Count}]" +
                $"TimeSlots:[{CommonLists.TimeSlotsList.Count}]");
        }
        public async Task<List<DeviceInfo>> GetDevicesListFromSqlAsync()
        {
            try
            {
                if (!_dataService.EquipmentRep.CheckConnection())
                    _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: Check connection error", LogLevel.Error);

                var equipmentList = await _dataService.EquipmentRep.GetAllAsync();

                var newlist = new List<DeviceInfo>();
                foreach (var eq in equipmentList)
                    newlist.Add(new DeviceInfo
                    {
                        Id = eq.Id,
                        DeviceID = eq.Code,
                        Name = eq.Name,
                        IP = eq.Ip,
                        Port = eq.Port,
                        TypeId = eq.TypeId,
                        Factory = eq.Factory,
                        Building = eq.Building,
                        Floor = eq.Floor,
                        Description = eq.Description
                    });
                return newlist;
            }
            catch (SqlException sqlex)
            {
                Debug.WriteLine($"SqlException: {sqlex.Message}"); 
                return new List<DeviceInfo>();
            }
            catch (TaskCanceledException taskex)
            {
                Debug.WriteLine($"TaskCanceledException: {taskex.Message}"); 
                return new List<DeviceInfo>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}"); 
                return new List<DeviceInfo>();
            }
        }
        public async Task<List<UserInfo>> GetUsersListFromSqlAsync()
        {
            try
            {
                if (!_dataService.EquipmentRep.CheckConnection())
                    _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: Check connection error", LogLevel.Error);

                var userList = await _dataService.EmployeeRep.GetAllAsync();

                var newlist = new List<UserInfo>();
                foreach (var us in userList)
                    newlist.Add(new UserInfo
                    {
                        Id = us.EmployeeId,
                        UserId = us.UserId,
                        Name = us.Name,
                        CardId = us.CardId,
                        Password = us.Password,
                        Email = us.Email,
                        RoleId = us.RoleId,
                        DepartmentId = us.DepartmentId
                    });
                return newlist;
            }
            catch (SqlException sqlex)
            {
                Debug.WriteLine($"SqlException: {sqlex.Message}");
                return new List<UserInfo>();
            }
            catch (TaskCanceledException taskex)
            {
                Debug.WriteLine($"TaskCanceledException: {taskex.Message}");
                return new List<UserInfo>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}");
                return new List<UserInfo>();
            }
        }
        public async Task<List<MaterialInfo>> GetMaterialsListFromSqlAsync()
        {
            try
            {
                if (!_dataService.MaterialRep.CheckConnection())
                    _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: Check connection error", LogLevel.Error);

                var materialList = await _dataService.MaterialRep.GetAllAsync();

                var newlist = new List<MaterialInfo>();
                foreach (var ma in materialList)
                    newlist.Add(new MaterialInfo
                    {
                        Id = ma.MaterialId,
                        Code = ma.MaterialCode,
                        Name = ma.Name,
                        Brand = ma.Brand,
                        Specification = ma.Specification,
                        TypeId = ma.TypeId,
                        Description = ma.Description,
                        MinimumStock = ma.MinimumStock,
                        QuantityInStock = ma.QuantityInStock
                    });
                return newlist;
            }
            catch (SqlException sqlex)
            {
                Debug.WriteLine($"SqlException: {sqlex.Message}");
                return new List<MaterialInfo>();
            }
            catch (TaskCanceledException taskex)
            {
                Debug.WriteLine($"TaskCanceledException: {taskex.Message}");
                return new List<MaterialInfo>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}");
                return new List<MaterialInfo>();
            }
        }
        public async Task<List<ErrorInfo>> GetErrorsListFromSqlAsync(string languageCode)
        {
            try
            {
                if (!_dataService.MaterialRep.CheckConnection())
                    _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: Check connection error", LogLevel.Error);

                var errorList = await _dataService.ErrorListRep.GetMessagesWithOtherAsync(languageCode);

                var newlist = new List<ErrorInfo>();
                foreach (var er in errorList)
                    newlist.Add(new ErrorInfo
                    {
                        ErrorCode = er.ErrorCode,
                        Message = er.Message,
                        Category = er.Category
                    });
                return newlist;
            }
            catch (SqlException sqlex)
            {
                Debug.WriteLine($"SqlException: {sqlex.Message}");
                return new List<ErrorInfo>();
            }
            catch (TaskCanceledException taskex)
            {
                Debug.WriteLine($"TaskCanceledException: {taskex.Message}");
                return new List<ErrorInfo>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}");
                return new List<ErrorInfo>();
            }
        }

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
                    var newvm = new DeviceCardContainerViewModel(_log, _dataService, _authService, 
                        CurrentUser, CommonLists);
                    Card1 = newvm;
                    break;

                case NavMode.View:
                    break;

                case NavMode.Setting:
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
