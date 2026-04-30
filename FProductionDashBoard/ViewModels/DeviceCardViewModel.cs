using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Properties;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.Exceptions;
using FProductionDashBoard.Services.V1;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.UserControls;
using MaterialDesignThemes.Wpf;
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
using static System.Formats.Asn1.AsnWriter;

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
        public DeviceInfo Info { get; }
        private readonly DashboardCoreServices _core;
        private readonly ListsFromSql _commonLists;

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

        // 調試計時
        [ObservableProperty]
        private bool isTuning = false;
        [ObservableProperty]
        private string tuningStatusText = string.Empty;
        private TuningType _activeTuningType;
        private int _tuningElapsedSeconds;
        private DispatcherTimer? _tuningTimer;

        // 介面邏輯
        public ICommand MaterialsChangeCommand { get; }
        public ICommand FirstInspectionCommand { get; }
        public ICommand RoutineInspectionCommand { get; }
        public ICommand OperationCommand { get; }
        public ICommand EndTuningCommand { get; }

#pragma warning disable CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮新增 'required' 修飾元，或將欄位宣告為可以為 Null。
        public DeviceCardViewModel() { }
#pragma warning restore CS8618 // 退出建構函式時，不可為 Null 的欄位必須包含非 Null 值。請考慮新增 'required' 修飾元，或將欄位宣告為可以為 Null。
        public DeviceCardViewModel(DashboardCoreServices core, DeviceInfo info, UserInfo currentuser, ListsFromSql getLists)
        {
            _core = core;
            Info = info;
            CurrentUser = currentuser;
            _commonLists = getLists;

            for (int i = 0; i < getLists.TimeSlotsList.Count; i++) { TimeSlotsStatus.Add(-1); }

            MaterialsChangeCommand = new AsyncRelayCommand(MaterialsChangeAsync);
            FirstInspectionCommand = new AsyncRelayCommand(FirstArticleInspectionAsync);
            RoutineInspectionCommand = new AsyncRelayCommand(RoutineInspectionAsync);
            OperationCommand = new AsyncRelayCommand(OperationAsync);
            EndTuningCommand = new AsyncRelayCommand(EndTuningAsync);

        }
        public async Task UpdateTimeSlotsStatusAsync()
        {
            try
            {
                var ideviceslots = await _core.Data.GetAllSlotsStatusAsync(_commonLists.TimeSlotsList, Info.Id);

                if (ideviceslots.Count != TimeSlotsStatus.Count)
                {
                    _core.Log.AddLog("時間區段數量有問題"); return;
                }

                for (int i = 0; i < ideviceslots.Count; i++)
                    TimeSlotsStatus[i] = ideviceslots[i];
            }
            catch (Exception ex)
            {
                _core.Log.AddLog($"{Properties.Resources.ComStrErrorTitle}: {ex.Message}", LogLevel.Error);
            }
        }
        // 操作員按鈕
        private async Task MaterialsChangeAsync() // 待翻譯
        {
            var vm = new MaterialDialogViewModel(Properties.Resources.DeviceMaterialDialog, this, _commonLists.MaterialsList);
            var uc = new MaterialsDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();

                    List<(int materialId, int quantity)> selectDetials = new List<(int, int)>();
                    foreach (var mdetial in result.Selections)
                        selectDetials.Add((mdetial.Id, mdetial.SelectedCount));

                    CurrentUser = _core.Authorization.CurrentUser!;
                    await _core.Data.AddReplacementRecordAsync(Info.Id, CurrentUser.Id, selectDetials);

                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"Category: {result.Selections.Count} -> " +
                        $"Sum: {result.Selections.Sum(d => d.SelectedCount)}", LogLevel.Success);
                }
                catch (OfflineOperationQueuedException)
                {
                    _core.Log.AddLog("物料更換已暫存，待連線恢復後自動上傳", LogLevel.Warning);
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog("物料更換紀錄上傳異常");
                    _core.Log.AddErrorLog($"MaterialsChange: {ex.Message}");
                    FirstInspectionStatus = false;
                }
            }
        }
        private async Task FirstArticleInspectionAsync() // 待翻譯
        {
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceFirstInsDialog, this, _commonLists.ErrorsList);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();
                    FirstInspectionStatus = result.IsNormal;

                    CurrentUser = _core.Authorization.CurrentUser!;
                    await _core.Data.AddFirstInspectionAsync(Info.Id, CurrentUser.Id, result.IsNormal,
                        CurrentProduct.Name, result.ErrorCode, result.Description);

                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"首件紀錄上傳完成");
                }
                catch (OfflineOperationQueuedException)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"首件紀錄已暫存，待連線恢復後自動上傳", LogLevel.Warning);
                }
                catch (BusinessRuleException ex)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"業務規則異常", LogLevel.Error);
                    _core.Log.AddErrorLog($"FirstInspection BusinessRuleEx: {ex.Message}");
                    FirstInspectionStatus = false;
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"首件紀錄上傳異常");
                    _core.Log.AddErrorLog($"FirstInspection Ex: {ex.Message}");
                    FirstInspectionStatus = false;
                }
            }
        }
        private async Task RoutineInspectionAsync() // 待翻譯
        {
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceRoutineInsDialog, this, _commonLists.ErrorsList);
            var uc = new InspectionDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();

                    var currentTimeSlot = _core.Data.GetCurrentTimeSlotId(_commonLists.TimeSlotsList);
                    if (currentTimeSlot == null)
                    {
                        _core.Log.AddLog("目前不在任何巡檢時段內");
                        return;
                    }

                    CurrentUser = _core.Authorization.CurrentUser!;
                    await _core.Data.AddRoutineInspectionAsync(Info.Id, CurrentUser.Id, result.IsNormal, currentTimeSlot ?? 1,
                        CurrentProduct.Name, result.ErrorCode, result.Description);
                    await UpdateTimeSlotsStatusAsync();
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"巡檢紀錄上傳完成");
                }
                catch (OfflineOperationQueuedException)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"巡檢紀錄已暫存，待連線恢復後自動上傳", LogLevel.Warning);
                }
                catch (BusinessRuleException ex)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"業務規則異常", LogLevel.Error);
                    _core.Log.AddErrorLog($"RoutineInspection BusinessRuleEx: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"巡檢紀錄上傳異常");
                    _core.Log.AddErrorLog($"RoutineInspection Ex: {ex.Message}");
                }
            }
        }
        private async Task OperationAsync()
        {
            CurrentUser = _core.Authorization.CurrentUser!;
            var vm = new TuningDialogViewModel(
                $"{Properties.Resources.ComStrDevice}: {Info.Name}",
                $"{Properties.Resources.ComStrUser}: {CurrentUser!.Name}",
                $"{Properties.Resources.ComStrProduct}: {CurrentProduct.Name}");
            var uc = new TuningDialog { DataContext = vm };
            var window = new DialogWindow(vm, uc);
            window.ShowDialog();

            if (!vm.IsConfirmed || vm.Result == null) return;

            _activeTuningType = vm.Result.TuningType;
            _tuningElapsedSeconds = 0;
            IsTuning = true;
            UpdateTuningText();

            _tuningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _tuningTimer.Tick += (_, _) => { _tuningElapsedSeconds++; UpdateTuningText(); };
            _tuningTimer.Start();
            await Task.CompletedTask;
        }

        // 調試視窗與結束事件
        private async Task EndTuningAsync()
        {
            if (!IsTuning) return;

            // 呼叫loading視窗 - 刷卡確認結束調試計時
            //var tcs = new TaskCompletionSource<bool>();
            bool confirmed = false;
            var loadingVm = new LoadingViewModel
            {
                Mode = LoadingMode.CardReader,
                Message = Properties.Resources.TuningCardConfirm,
                CanCancel = true
            };
            var loadingWin = new LoadingWindow(loadingVm);

            // 讀卡機事件 - 確認是否與當前登入人員一致
            void OnCardConfirm(object? s, CardReadEventArgs e)
            {
                if (e.CardId == CurrentUser.CardId)
                {
                    //tcs.TrySetResult(true);
                    Application.Current.Dispatcher.Invoke(() => {
                        confirmed = true;
                        loadingWin.Close();
                    });
                }
            }

            // loadingVm.CloseRequested += (_, _) => tcs.TrySetResult(false);
            _core.CardReader.ResetLastCard();
            _core.CardReader.CardRead += OnCardConfirm;
            //開啟並等待視窗
            loadingWin.ShowDialog();
            // bool confirmed = await tcs.Task;
            _core.CardReader.CardRead -= OnCardConfirm;
            // loadingWin.Close();

            if (!confirmed) return;

            // 調試紀錄流程
            try
            {
                _tuningTimer?.Stop();
                IsTuning = false;
                int elapsed = _tuningElapsedSeconds;

                if (_activeTuningType == TuningType.Teaching)
                    await _core.Data.AddTeachingRecordAsync(Info.Id, CurrentUser.Id, elapsed, CurrentProduct?.Name);
                else
                    await _core.Data.AddOffsetRecordAsync(Info.Id, CurrentUser.Id, elapsed, CurrentProduct?.Name);

                var elapsedStr = TimeSpan.FromSeconds(elapsed);
                _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                    $"調試紀錄上傳完成（{_activeTuningType}，{elapsedStr:hh\\:mm\\:ss}）", LogLevel.Success);
            }
            catch (OfflineOperationQueuedException)
            {
                _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                    $"調試紀錄已暫存，待連線恢復後自動上傳", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                _core.Log.AddErrorLog($"EndTuning Ex: {ex.Message}");
            }
        }
        private void UpdateTuningText()
        {
            var label = _activeTuningType == TuningType.Teaching
                ? Properties.Resources.TuningInProgressTeaching
                : Properties.Resources.TuningInProgressOffset;
            var ts = TimeSpan.FromSeconds(_tuningElapsedSeconds);
            TuningStatusText = $"{label} {ts:hh\\:mm\\:ss}";
        }

    }
}
