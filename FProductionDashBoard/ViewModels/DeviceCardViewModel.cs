using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Properties;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.UserControls;
using MaterialDesignThemes.Wpf;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FProductionDashBoard.ViewModels
{
    public enum UserAction
    {
        Producing = 0,
        Tuning = 1,
        Maintaining = 2
    }
    
    public partial class DeviceCardViewModel : ObservableObject
    {
        private DispatcherTimer checkTimer;
        public DeviceInfo Info { get; }
        private readonly LogService _log;
        private readonly IDataService _dataService;

        private List<MaterialInfo> sqlMaterialsList = new(); // 物料清單 (資料表來源)
        //private List<DeviceInfo> sqlErrorsList = new(); // 異常項目清單 (資料表來源)
        //private List<DeviceInfo> sqlOrdersList = new(); // 排單點檢清單 (資料表來源)
        //private List<DeviceInfo> sqlEmployeesList = new(); // 人員清單 (資料表來源)

        // 訊息顯示
        [ObservableProperty]
        private UserInfo currentUser = new() { ID = "none", Name = "none" };
        [ObservableProperty]
        private ProductInfo currentProduct = new() { ModelCode = "Unknown", TypeCode = "123" };

        public InspectionService InspectionStatuses { get; } // 品檢
        [ObservableProperty]
        private int currentAction = (int)UserAction.Producing; // 調試狀態

        // 介面邏輯
        public ICommand MaterialsChangeCommand { get; }
        public ICommand FirstInspectionCommand { get; }
        public ICommand RoutineInspectionCommand { get; }
        public ICommand OperationCommand { get; }

        public DeviceCardViewModel(DeviceInfo info, UserInfo currentuser, LogService log, IDataService dataservice)
        {
            Info = info;
            _log = log;
            _dataService = dataservice;
            CurrentUser = currentuser;

            InspectionStatuses = new InspectionService(this, 9, 4, 2);
            InspectionStatuses.OnLogEvent += _log.AddLog;
            //InspectionStatuses.OnErrorLogEvent += _log.AddErrorLog;

            MaterialsChangeCommand = new AsyncRelayCommand(MaterialsChange);
            FirstInspectionCommand = new RelayCommand(FirstArticleInspection);
            RoutineInspectionCommand = new RelayCommand(RoutineInspection);
            OperationCommand = new RelayCommand(OperationChange);

            // 巡檢用計時
            checkTimer = new DispatcherTimer();
            checkTimer.Interval = TimeSpan.FromSeconds(1);
            checkTimer.Tick += (s, e) => { InspectionStatuses.checkRoutineTime(); };
            checkTimer.Start();
        }
        // 操作員按鈕
        private async Task MaterialsChange()
        {
            await GetDevicesListFromSqlAsync();
            var vm = new MaterialDialogViewModel(Properties.Resources.DeviceMaterialDialog, this, sqlMaterialsList);
            var uc = new MaterialsDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new MaterialResult() { Selections = new List<MaterialInfo>() };
                // SQL
                _log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - Category: {result.Selections.Count} -> " +
                    $"Sum: {result.Selections.Sum(d => d.SelectedCount)}", LogLevel.Success);
            }
        }
        private void FirstArticleInspection()
        {
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceFirstInsDialog, this);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new() { IsNormal = false };
                InspectionStatuses.updateFirstInspection(result.IsNormal, result.Description); // SQL
            }
        }
        private void RoutineInspection()
        {
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceRoutineInsDialog, this);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                var result = vm.Result ?? new InspectionResult() { IsNormal = false };
                int statusresult = result.IsNormal ? 0 : 2;
                InspectionStatuses.updateRoutineStatus(statusresult, result.Description); // SQL
            }
        }
        private void OperationChange()
        {
            //var vm = new DialogBaseViewModel<string>(Properties.Resources.DeviceOperationDialog);
            //var window = new DialogWindow(vm);
            //window.ShowDialog(); 
            //if (vm.IsConfirmed)
            //{ var result = vm.Result; }
            Info.Status++;
            if (Info.Status > 2) Info.Status = -1;
            _log.AddLog($"設備 {Info.Name} 設備調試狀態更新:", LogLevel.Processing);
            _log.AddLog($"設備 {Info.Name} 設備調試狀態更新:", LogLevel.Info);
            _log.AddLog($"設備 {Info.Name} 設備調試狀態更新:", LogLevel.Warning);
            _log.AddLog($"設備 {Info.Name} 設備調試狀態更新:", LogLevel.Error);
            _log.AddLog($"設備 {Info.Name} 設備調試狀態更新:", LogLevel.Success);
            _log.AddErrorLog($"設備 {Info.Name} 設備調試狀態更新:");
        }
        // sql 清單取得 (物料 品檢異常清單 排單 點檢清單 人員權限)
        public async Task GetDevicesListFromSqlAsync() // 待翻譯log 並加上errorlog
        {
            try
            {
                if (!_dataService.MaterialRep.CheckConnection())
                    _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: Check connection error", LogLevel.Error);

                var materialList = (await _dataService.MaterialRep.GetAllAsync());

                if (sqlMaterialsList.Any()) sqlMaterialsList.Clear();
                foreach (var eq in materialList)
                    sqlMaterialsList.Add(new MaterialInfo
                    {
                        Id = eq.MaterialId,
                        Code = eq.MaterialCode,
                        Name = eq.Name,
                        Brand = eq.Brand,
                        Specification = eq.Specification,
                        TypeId = eq.TypeId,
                        Description = eq.Description,
                        MinimumStock = eq.MinimumStock,
                        QuantityInStock = eq.QuantityInStock
                    });
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
