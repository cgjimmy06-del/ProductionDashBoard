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
        private readonly Services.IDialogService _dialog;

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
        public ICommand TuningCommand { get; }
        public ICommand EndTuningCommand { get; }

        public DeviceCardViewModel(DashboardCoreServices core, Services.IDialogService dialog, DeviceInfo info, UserInfo currentUser, ListsFromSql getLists)
        {
            _core = core;
            _dialog = dialog;
            Info = info;
            CurrentUser = currentUser;
            _commonLists = getLists;

            for (int i = 0; i < getLists.TimeSlotsList.Count; i++) { TimeSlotsStatus.Add(-1); }

            MaterialsChangeCommand = new AsyncRelayCommand(MaterialsChangeAsync,
                () => _core.Authorization.HasPermission(PermissionId.OperateMaterial));
            FirstInspectionCommand = new AsyncRelayCommand(FirstArticleInspectionAsync,
                () => _core.Authorization.HasPermission(PermissionId.OperateInspection));
            RoutineInspectionCommand = new AsyncRelayCommand(RoutineInspectionAsync,
                () => _core.Authorization.HasPermission(PermissionId.OperateInspection));
            TuningCommand = new AsyncRelayCommand(TuningAsync, 
                () => _core.Authorization.HasPermission(PermissionId.OperateTuning));
            EndTuningCommand = new AsyncRelayCommand(EndTuningAsync, 
                () => _core.Authorization.HasPermission(PermissionId.OperateTuning));

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
                    Application.Current.Dispatcher.Invoke(() => TimeSlotsStatus[i] = ideviceslots[i]);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog($"[{Info.Name}] 時段狀態更新失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[UpdateTimeSlotsStatusAsync] {ex.Message}");
            }
        }
        // 操作員按鈕
        private async Task MaterialsChangeAsync()
        {
            CurrentUser = _core.Authorization.CurrentUser!;
            var vm = new MaterialDialogViewModel(Properties.Resources.DeviceMaterialDialog, this, _commonLists.MaterialsList);
            _dialog.ShowDialog(vm);

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();

                    List<(int materialId, int quantity)> selectDetials = new List<(int, int)>();
                    foreach (var mdetial in result.Selections)
                        selectDetials.Add((mdetial.Id, mdetial.SelectedCount));

                    await _core.Data.AddReplacementRecordAsync(Info.Id, CurrentUser.Id, selectDetials);

                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        $"Category: {result.Selections.Count} -> " +
                        $"Sum: {result.Selections.Sum(d => d.SelectedCount)}", LogLevel.Success);
                }
                catch (OfflineOperationQueuedException)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " + 
                        "物料更換已暫存，待連線恢復後自動上傳", LogLevel.Warning);
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - 物料更換紀錄上傳失敗", LogLevel.Error);
                    _core.Log.AddErrorLog($"[MaterialsChangeAsync] {ex.Message}");
                    FirstInspectionStatus = false;
                }
            }
        }
        private async Task FirstArticleInspectionAsync()
        {
            CurrentUser = _core.Authorization.CurrentUser!;
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceFirstInsDialog, this, _commonLists.ErrorsList);
            _dialog.ShowDialog(vm);

            if (vm.IsConfirmed)
            {
                try
                {
                    var result = vm.Result ?? new();
                    FirstInspectionStatus = result.IsNormal;

                    await _core.Data.AddFirstInspectionAsync(Info.Id, CurrentUser.Id, result.IsNormal,
                        CurrentProduct.ProductId, result.ErrorCode, result.Description);

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
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - 首件業務規則異常", LogLevel.Error);
                    _core.Log.AddErrorLog($"[FirstArticleInspectionAsync] {ex.Message}");
                    FirstInspectionStatus = false;
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - 首件紀錄上傳失敗", LogLevel.Error);
                    _core.Log.AddErrorLog($"[FirstArticleInspectionAsync] {ex.Message}");
                    FirstInspectionStatus = false;
                }
            }
        }
        private async Task RoutineInspectionAsync()
        {
            CurrentUser = _core.Authorization.CurrentUser!;
            var vm = new InspectionDialogViewModel(Properties.Resources.DeviceRoutineInsDialog, this, _commonLists.ErrorsList);
            _dialog.ShowDialog(vm);

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

                    await _core.Data.AddRoutineInspectionAsync(Info.Id, CurrentUser.Id, result.IsNormal, currentTimeSlot ?? 1,
                        CurrentProduct.ProductId, result.ErrorCode, result.Description);
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
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - 巡檢業務規則異常", LogLevel.Error);
                    _core.Log.AddErrorLog($"[RoutineInspectionAsync] {ex.Message}");
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - 巡檢紀錄上傳失敗", LogLevel.Error);
                    _core.Log.AddErrorLog($"[RoutineInspectionAsync] {ex.Message}");
                }
            }
        }
        private async Task TuningAsync()
        {
            CurrentUser = _core.Authorization.CurrentUser!;
            var vm = new TuningDialogViewModel(
                $"{Properties.Resources.ComStrDevice}: {Info.Name}",
                $"{Properties.Resources.ComStrUser}: {CurrentUser!.Name}",
                $"{Properties.Resources.ComStrProduct}: {CurrentProduct.Name}");
            _dialog.ShowDialog(vm);

            if (!vm.IsConfirmed || vm.Result == null) return;

            _activeTuningType = vm.Result.TuningType;
            _tuningElapsedSeconds = 0;
            IsTuning = true;
            UpdateTuningText();

            if (_tuningTimer != null)
            {
                _tuningTimer.Stop();
                _tuningTimer.Tick -= OnTuningTimerTick;
            }
            _tuningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _tuningTimer.Tick += OnTuningTimerTick;
            _tuningTimer.Start();
            await Task.CompletedTask;
        }

        // 調試視窗與結束事件
        private async Task EndTuningAsync()
        {
            if (!IsTuning) return;

            // 呼叫loading視窗 - 刷卡確認結束調試計時
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
                    Application.Current.Dispatcher.BeginInvoke(() => {
                        confirmed = true;
                        loadingWin.Close();
                    });
                }
            }

            _core.CardReader.ResetLastCard();
            _core.CardReader.CardRead += OnCardConfirm;

            //開啟並等待視窗
            loadingWin.ShowDialog();
            _core.CardReader.CardRead -= OnCardConfirm;

            if (!confirmed) return;

            // 調試紀錄流程
            try
            {
                if (_tuningTimer != null)
                {
                    _tuningTimer.Stop();
                    _tuningTimer.Tick -= OnTuningTimerTick;
                    _tuningTimer = null;
                }
                IsTuning = false;
                int elapsed = _tuningElapsedSeconds;

                if (_activeTuningType == TuningType.Teaching)
                    await _core.Data.AddTeachingRecordAsync(Info.Id, CurrentUser.Id, elapsed, CurrentProduct?.ProductId);
                else
                    await _core.Data.AddOffsetRecordAsync(Info.Id, CurrentUser.Id, elapsed, CurrentProduct?.ProductId);

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
                _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - 調試紀錄上傳失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[EndTuningAsync] {ex.Message}");
            }
        }
        private void OnTuningTimerTick(object? s, EventArgs e)
        {
            _tuningElapsedSeconds++;
            UpdateTuningText();
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
