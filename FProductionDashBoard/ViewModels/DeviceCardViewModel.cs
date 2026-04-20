using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Properties;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.V1;
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
        private readonly AuthorizationService _authService;
        private readonly ListsFromSql commonLists;

        // 訊息顯示
        [ObservableProperty]
        private UserInfo currentUser = new() { UserId = "none", Name = "none" };
        [ObservableProperty]
        private ProductInfo currentProduct = new() { ModelCode = "Unknown", TypeCode = "123" };

        // 操作按鈕及狀態顯示
        [ObservableProperty]
        private bool isSelected = false; // 是否被選擇
        [ObservableProperty]
        private bool routineCycleEnable = false; // 是否開啟巡檢功能
        [ObservableProperty]
        private bool firstInspectionStatus = false; // 首件狀態
        public ObservableCollection<int> TimeSlotsStatus { get; } = new ObservableCollection<int>(); // 各時段狀態
        [ObservableProperty]
        private int currentAction = (int)UserAction.Producing; // 調試狀態

        // 介面邏輯
        public ICommand MaterialsChangeCommand { get; }
        public ICommand FirstInspectionCommand { get; }
        public ICommand RoutineInspectionCommand { get; }
        public ICommand OperationCommand { get; }

#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮新增 'required' 修飾元，或將欄位宣告為可以為 Null。
        public DeviceCardViewModel() { }
#pragma warning restore CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮新增 'required' 修飾元，或將欄位宣告為可以為 Null。
        public DeviceCardViewModel(LogService log, IDataService dataservice, AuthorizationService authService,
            DeviceInfo info, UserInfo currentuser, ListsFromSql getLists)
        {
            _log = log;
            _dataService = dataservice;
            _authService = authService;
            Info = info;
            CurrentUser = currentuser;
            commonLists = getLists;

            for (int i = 0; i < getLists.TimeSlotsList.Count; i++) { TimeSlotsStatus.Add(-1); }

            MaterialsChangeCommand = new AsyncRelayCommand(MaterialsChange);
            FirstInspectionCommand = new AsyncRelayCommand(FirstArticleInspection);
            RoutineInspectionCommand = new AsyncRelayCommand(RoutineInspection);
            OperationCommand = new RelayCommand(OperationChange);

            // 巡檢用計時
            checkTimer = new DispatcherTimer();
            //checkTimer.Interval = TimeSpan.FromSeconds(1);
            //checkTimer.Tick += (s, e) => {  };
            //checkTimer.Start();
        }
        public async Task UpdateTimeSlotsStatusAsync()
        {
            var ideviceslots = await _dataService.GetAllSlotsStatusAsync(commonLists.TimeSlotsList, Info.Id);

            if (ideviceslots.Count != TimeSlotsStatus.Count) {
                _log.AddLog("時間區段數量有問題"); return; }

            for(int i = 0;i < ideviceslots.Count;i++)
                TimeSlotsStatus[i] = ideviceslots[i];
        }
        // 操作員按鈕
        private async Task MaterialsChange() // 待翻譯
        {
            var vm = new MaterialDialogViewModel(Properties.Resources.DeviceMaterialDialog, this, commonLists.MaterialsList);
            var uc = new MaterialsDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();

                    List<(int materialId, int quantity)> selectdetials = new List<(int, int)>();
                    foreach (var mdetial in result.Selections)
                        selectdetials.Add((mdetial.Id, mdetial.SelectedCount));

                    await _dataService.AddReplacementRecordAsync(Info.Id, CurrentUser.Id, selectdetials);

                    _log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"Category: {result.Selections.Count} -> " +
                        $"Sum: {result.Selections.Sum(d => d.SelectedCount)}", LogLevel.Success);
                }
                catch (Exception ex)
                {
                    _log.AddLog("物料更換紀錄上傳異常");
                    _log.AddErrorLog($"MaterialsChange: {ex.Message}");
                    FirstInspectionStatus = false;
                }
            }
        }
        private async Task FirstArticleInspection() // 待翻譯
        {
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceFirstInsDialog, this, commonLists.ErrorsList);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();
                    await _dataService.AddFirstInspectionAsync(Info.Id, CurrentUser.Id, result.IsNormal,
                        CurrentProduct.Name, result.ErrorCode, result.Description);

                    FirstInspectionStatus = result.IsNormal;
                    _log.AddLog("首件紀錄上傳完成");
                }
                catch (Exception ex)
                {
                    _log.AddLog("首件紀錄上傳異常");
                    _log.AddErrorLog($"FirstArticleInspection: {ex.Message}");
                    FirstInspectionStatus = false;
                }
            }
        }
        private async Task RoutineInspection() // 待翻譯
        {
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceRoutineInsDialog, this, commonLists.ErrorsList);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();

                    var currentTimeSlot = await _dataService.GetCurrentTimeSlotIdAsync();
                    if (currentTimeSlot == null)
                    {
                        _log.AddLog("目前不在任何巡檢時段內");
                        return;
                    }

                    await _dataService.AddRoutineInspectionAsync(Info.Id, CurrentUser.Id, result.IsNormal, currentTimeSlot ?? 1,
                        CurrentProduct.Name, result.ErrorCode, result.Description);
                    await UpdateTimeSlotsStatusAsync();
                    _log.AddLog("巡檢紀錄上傳完成");
                }
                catch (Exception ex)
                {
                    _log.AddLog("巡檢紀錄上傳異常");
                    _log.AddErrorLog($"RoutineInspection: {ex.Message}");
                }
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

    }
}
