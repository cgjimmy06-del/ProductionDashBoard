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
        private ProductInfo? currentProduct = null;

        // 接單
        [ObservableProperty] private bool isProducing = false;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCurrentTimeSlot))]
        private string currentProductDisplayName = string.Empty;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCurrentTimeSlot))]
        private string currentTimeSlotLabel = string.Empty;
        public bool HasCurrentTimeSlot => !string.IsNullOrEmpty(CurrentTimeSlotLabel);
        public ObservableCollection<OrderProductionInfo> Orders { get; } = new();
        private OrderProductionInfo? _activeOrder;

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
        [ObservableProperty] private bool isTuning = false;
        [ObservableProperty] private string tuningStatusText = string.Empty;
        [ObservableProperty] private string tuningUserName = string.Empty;
        [ObservableProperty] private string tuningProductLabel = string.Empty;
        private TuningType _activeTuningType;
        private int _tuningElapsedSeconds;
        private DispatcherTimer? _tuningTimer;
        private int? _activeProgramTuningId;
        private UiModels.UserInfo? _activeTuningStartedByEmployee;

        // 介面邏輯
        public ICommand OrderCommand { get; }
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

            CurrentProductDisplayName = Resources.NoCurrentProduct;

            OrderCommand = new AsyncRelayCommand(OpenOrderDialogAsync,
                () => _core.Authorization.HasPermission(PermissionId.Order));
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

            _ = LoadOrdersAsync();
            _ = LoadProgramTuningStateAsync();
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

                RefreshCurrentTimeSlotLabel();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog($"[{Info.Name}] 時段狀態更新失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[UpdateTimeSlotsStatusAsync] {ex.Message}");
            }
        }

        // ─── 接單服務 ─────────────────────────────────────────────────────────

        private async Task LoadOrdersAsync()
        {
            try
            {
                var entities = await _core.Data.GetOrdersByEquipmentAsync(Info.Id);
                var mapped = entities.Select(OrderProductionInfo.FromEntity).ToList();
                var active = mapped.FirstOrDefault(o => o.IsInProduction);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Orders.Clear();
                    foreach (var o in mapped) Orders.Add(o);
                });

                ApplyProductionState(active);
            }
            catch (Exception ex)
            {
                _core.Log.AddErrorLog($"[LoadOrdersAsync] {ex.Message}");
            }
        }

        private async Task OpenOrderDialogAsync()
        {
            var vm = new OrderListDialogViewModel(
                _core,
                Info.Id,
                _commonLists.EquipmentProductsList
                    .Where(ep => ep.EquipmentId == Info.Id).ToList(),
                CurrentUser,
                $"{Properties.Resources.OrderListTitle}: {Info.Name}");
            _dialog.ShowDialog(vm);

            if (vm.IsConfirmed && vm.Result != null)
            {
                try
                {
                    switch (vm.Result.Action)
                    {
                        case OrderListAction.StartProduction:
                            await _core.Data.StartProductionAsync(vm.Result.OrderId, CurrentUser.Id);
                            break;
                        case OrderListAction.EndProduction:
                            await _core.Data.EndProductionAsync(vm.Result.OrderId);
                            break;
                        case OrderListAction.CancelProduction:
                            await _core.Data.CancelOrderAsync(vm.Result.OrderId, vm.Result.Description);
                            break;
                    }
                }
                catch (OfflineOperationQueuedException)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                        "操作已暫存，待連線恢復後自動上傳", LogLevel.Warning);
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - 接單操作失敗", LogLevel.Error);
                    _core.Log.AddErrorLog($"[OpenOrderDialogAsync] {ex.Message}");
                }
            }
            await LoadOrdersAsync();
        }

        private void ApplyProductionState(OrderProductionInfo? order)
        {
            _activeOrder = order;
            if (order != null)
            {
                IsProducing = true;
                CurrentProductDisplayName = $"{order.ProductName} · {order.ProcessName}";
                if (CurrentProduct == null) CurrentProduct = new ProductInfo();
                CurrentProduct.ProductId = order.SopProductId;
                CurrentProduct.ProductName = CurrentProductDisplayName;
            }
            else
            {
                IsProducing = false;
                CurrentProductDisplayName = Resources.NoCurrentProduct;
                if (CurrentProduct != null) CurrentProduct = null;
            }
        }

        private void RefreshCurrentTimeSlotLabel()
        {
            var slotId = _core.Data.GetCurrentTimeSlotId(_commonLists.TimeSlotsList);
            CurrentTimeSlotLabel = slotId.HasValue
                ? _commonLists.TimeSlotsList.FirstOrDefault(s => s.TimeSlotId == slotId)?.Label ?? string.Empty
                : string.Empty;
        }

        // ─── 操作員按鈕 ─────────────────────────────────────────────────────

        private async Task MaterialsChangeAsync()
        {
            CurrentUser = _core.Authorization.CurrentUser!;

            List<MaterialInfo> materials = _commonLists.MaterialsList;
            if (_activeOrder != null)
            {
                try
                {
                    var checklist = await _core.Data.GetSopChecklistWithItemsAsync(_activeOrder.SopId);
                    var sopMaterialIds = checklist?.Items
                        .Where(i => i.CheckType == CheckType.Station || i.CheckType == CheckType.Fixture)
                        .Select(i => i.MaterialId)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToHashSet() ?? new HashSet<int>();
                    if (sopMaterialIds.Count > 0)
                        materials = _commonLists.MaterialsList.Where(m => sopMaterialIds.Contains(m.Id)).ToList();
                }
                catch (Exception ex)
                {
                    _core.Log.AddErrorLog($"[MaterialsChangeAsync] SOP 物料載入失敗，改為顯示全部：{ex.Message}");
                }
            }

            var vm = new MaterialDialogViewModel(Properties.Resources.DeviceMaterialDialog, this, materials);
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
                        CurrentProduct?.ProductId, result.ErrorCode, result.Description);

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
                        CurrentProduct?.ProductId, result.ErrorCode, result.Description);
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

            var items = _commonLists.EquipmentProductsList
                .Where(ep => ep.EquipmentId == Info.Id)
                .OrderBy(ep => ep.SeqNo)
                .Select(ep => new EquipmentProductItem
                {
                    EquipmentProductId = ep.EquipmentProductId,
                    SopId = ep.SopId,
                    SeqNo = ep.SeqNo,
                    DisplayLabel = BuildTuningProductLabel(ep),
                    ProductionStatus = ep.ProductionStatus
                })
                .ToList();

            var vm = new TuningDialogViewModel(
                $"{Properties.Resources.ComStrDevice}: {Info.Name}",
                $"{Properties.Resources.ComStrUser}: {CurrentUser!.Name}",
                items);
            _dialog.ShowDialog(vm);

            if (!vm.IsConfirmed || vm.Result == null) return;

            var result = vm.Result;
            _activeTuningType = result.TuningType;
            _tuningElapsedSeconds = 0;

            try
            {
                _activeProgramTuningId = await _core.Data.StartProgramTuningAsync(
                    Info.Id, result.EquipmentProductId, result.TuningType, CurrentUser.Id, DateTime.Now);
                _activeTuningStartedByEmployee = CurrentUser;
                TuningUserName = CurrentUser.Name;
                TuningProductLabel = items.FirstOrDefault(i => i.EquipmentProductId == result.EquipmentProductId)?.DisplayLabel ?? string.Empty;
            }
            catch (Exception ex)
            {
                _core.Log.AddLog($"[{Info.Name}] 調試啟動失敗，請檢查連線", LogLevel.Error);
                _core.Log.AddErrorLog($"[TuningAsync] {ex.Message}");
                return;
            }

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
        }

        // 調試視窗與結束事件
        private async Task EndTuningAsync()
        {
            if (!IsTuning) return;

            bool confirmed = false;
            UiModels.UserInfo? endedBy = null;

            var loadingVm = new LoadingViewModel
            {
                Mode = LoadingMode.CardReader,
                Message = Properties.Resources.TuningCardConfirm,
                CanCancel = true
            };
            var loadingWin = new LoadingWindow(loadingVm);

            void OnCardConfirm(object? s, CardReadEventArgs e)
            {
                // 原始啟動者
                if (e.CardId == _activeTuningStartedByEmployee?.CardId)
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        confirmed = true;
                        endedBy = _activeTuningStartedByEmployee;
                        loadingWin.Close();
                    });
                }
                else
                {
                    // 具 Setting 權限者
                    var userInfo = _commonLists.UsersList.FirstOrDefault(u => u.CardId == e.CardId);
                    if (userInfo != null)
                    {
                        var role = _commonLists.RolesList.FirstOrDefault(r => r.RoleId == userInfo.RoleId);
                        if (role?.RolePermissions.Any(rp => rp.PermissionId == PermissionId.Setting) == true)
                        {
                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                confirmed = true;
                                endedBy = userInfo;
                                loadingWin.Close();
                            });
                        }
                    }
                }
            }

            _core.CardReader.ResetLastCard();
            _core.CardReader.CardRead += OnCardConfirm;
            loadingWin.ShowDialog();
            _core.CardReader.CardRead -= OnCardConfirm;

            if (!confirmed) return;

            if (_tuningTimer != null)
            {
                _tuningTimer.Stop();
                _tuningTimer.Tick -= OnTuningTimerTick;
                _tuningTimer = null;
            }
            IsTuning = false;
            int elapsed = _tuningElapsedSeconds;

            try
            {
                string? description = null;
                if (endedBy?.Id != _activeTuningStartedByEmployee?.Id)
                {
                    var typeLabel = _activeTuningType == TuningType.Teaching
                        ? Properties.Resources.TuningInProgressTeaching
                        : Properties.Resources.TuningInProgressOffset;
                    description = $"由{endedBy?.Name}結束{typeLabel}";
                }

                if (_activeProgramTuningId.HasValue)
                    await _core.Data.EndProgramTuningAsync(_activeProgramTuningId.Value, DateTime.Now, description);

                var elapsedStr = TimeSpan.FromSeconds(elapsed);
                _core.Log.AddLog($"{Properties.Resources.ComStrDevice}:{Info.Name} - " +
                    $"調試完成（{_activeTuningType}，{elapsedStr:hh\\:mm\\:ss}）", LogLevel.Success);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog($"[{Info.Name}] 調試結束失敗，請檢查連線", LogLevel.Error);
                _core.Log.AddErrorLog($"[EndTuningAsync] {ex.Message}");
            }
            finally
            {
                _activeProgramTuningId = null;
                _activeTuningStartedByEmployee = null;
                TuningUserName = string.Empty;
                TuningProductLabel = string.Empty;
            }
        }

        private async Task LoadProgramTuningStateAsync()
        {
            try
            {
                var record = await _core.Data.GetInProgressProgramTuningAsync(Info.Id);
                if (record == null) return;

                _activeProgramTuningId = record.ProgramTuningId;
                _activeTuningType = record.TuningType;
                _activeTuningStartedByEmployee = _commonLists.UsersList.FirstOrDefault(u => u.Id == record.StartedBy);
                _tuningElapsedSeconds = (int)(DateTime.Now - record.StartedAt).TotalSeconds;
                TuningUserName = record.StartedByEmployee?.Name ?? string.Empty;
                TuningProductLabel = BuildTuningProductLabel(record.EquipmentProductNav);
                IsTuning = true;
                UpdateTuningText();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _tuningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    _tuningTimer.Tick += OnTuningTimerTick;
                    _tuningTimer.Start();
                });
            }
            catch (Exception ex)
            {
                _core.Log.AddErrorLog($"[LoadProgramTuningStateAsync] {ex.Message}");
            }
        }

        private static string BuildTuningProductLabel(EquipmentProduct? ep)
        {
            if (ep?.Sop?.Product == null) return string.Empty;
            var product = ep.Sop.Product;
            var productName = $"{product.Part?.PartNo}_{product.Model?.Name}";
            var processName = ep.Sop.Process?.Name ?? string.Empty;
            return $"#{ep.SeqNo}  {productName} · {processName}";
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
