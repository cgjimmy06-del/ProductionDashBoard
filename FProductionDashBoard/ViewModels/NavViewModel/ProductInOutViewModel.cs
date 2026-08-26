using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FProductionDashBoard.ViewModels
{
    public enum ProductInOutPanelMode { Detail, Form }

    public partial class ProductInOutViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
        private readonly IDialogService _dialog;
        private readonly List<ScheduleUiModel> _all = new();

        // 統計
        [ObservableProperty] private int statTotal;
        [ObservableProperty] private int statTotalQty;
        [ObservableProperty] private int statPending;
        [ObservableProperty] private int statPendingQty;
        [ObservableProperty] private int statPendingDevelopCount;
        [ObservableProperty] private int statPendingDevelopQty;
        [ObservableProperty] private int statScheduled;
        [ObservableProperty] private int statScheduledQty;
        [ObservableProperty] private int statCompleted;
        [ObservableProperty] private int statCompletedQty;
        [ObservableProperty] private int statReleased;
        [ObservableProperty] private int statReleasedQty;

        // 篩選
        [ObservableProperty] private ScheduleStatus? statusFilter;
        [ObservableProperty] private string sopFilter = "All";
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private string processFilter = string.Empty;

        // 日期篩選
        [ObservableProperty] private DateTime? dateRangeStart;
        [ObservableProperty] private DateTime? dateRangeEnd;
        [ObservableProperty] private bool isFilterPanelVisible = true;

        public ICollectionView SchedulesView { get; }

        // 右側面板
        [ObservableProperty] private bool isPanelVisible;
        [ObservableProperty] private ProductInOutPanelMode panelMode;
        [ObservableProperty] private ScheduleUiModel? selectedSchedule;

        // 入料表單
        [ObservableProperty] private string formLotNo = string.Empty;
        [ObservableProperty] private string formPartFilter = string.Empty;
        [ObservableProperty] private string formModelFilter = string.Empty;
        [ObservableProperty] private string formProcessFilter = string.Empty;
        [ObservableProperty] private int formQuantity;
        [ObservableProperty] private string formDescription = string.Empty;
        [ObservableProperty] private string? formError;
        [ObservableProperty] private int? formLocationId;   // 入料選填倉位；null = (無) 不指派

        private List<Product> _allProducts = new();
        public ObservableCollection<Product> FilteredProducts { get; } = new();
        [ObservableProperty] private Product? selectedProduct;

        private List<WorkProcess> _allProcesses = new();
        public ObservableCollection<WorkProcess> FilteredProcesses { get; } = new();
        [ObservableProperty] private WorkProcess? selectedProcess;

        // 倉位下拉來源（首項「(無)」+ 各啟用倉位），入料表單與改倉 Dialog 共用；載入時整批重建
        [ObservableProperty] private IReadOnlyList<PioLocationChoice> locationChoices = System.Array.Empty<PioLocationChoice>();

        public IReadOnlyList<ScheduleStatusFilterOption> StatusFilterOptions { get; }
        public IReadOnlyList<PioSopFilterOption> SopFilterOptions { get; }

        partial void OnStatusFilterChanged(ScheduleStatus? value) => ApplyFilter();
        partial void OnSopFilterChanged(string value) => ApplyFilter();
        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnProcessFilterChanged(string value) => ApplyFilter();
        partial void OnFormPartFilterChanged(string value) => RebuildFilteredProducts();
        partial void OnFormModelFilterChanged(string value) => RebuildFilteredProducts();
        partial void OnFormProcessFilterChanged(string value) => RebuildFilteredProcesses();

        partial void OnDateRangeStartChanged(DateTime? value)
        {
            if (value.HasValue && (DateRangeEnd == null || DateRangeEnd < value))
                DateRangeEnd = value;
            ComputeStats();
            SchedulesView?.Refresh();
        }

        partial void OnDateRangeEndChanged(DateTime? value)
        {
            ComputeStats();
            SchedulesView?.Refresh();
        }

        public ProductInOutViewModel(DashboardCoreServices core, IDialogService dialog)
        {
            _core = core;
            _dialog = dialog;
            StatusFilterOptions = new ScheduleStatusFilterOption[] { new(null) }
                .Concat(Enum.GetValues<ScheduleStatus>().Select(s => new ScheduleStatusFilterOption(s)))
                .ToArray();
            SopFilterOptions = new PioSopFilterOption[] { new("All"), new("New"), new("Develop"), new("Mass") };

            SchedulesView = CollectionViewSource.GetDefaultView(_all);
            SchedulesView.Filter = FilterSchedule;
            SchedulesView.SortDescriptions.Add(
                new SortDescription(nameof(ScheduleUiModel.ScheduleId), ListSortDirection.Descending));

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var schedules = await _core.Data.GetAllSchedulesAsync();
                ReplaceAll(schedules);
                await LoadLocationChoicesAsync();   // 倉位下拉來源（含「(無)」）
                await ApplyLocationsAsync();         // 設定各列 LocationId/LocationCode（須在 Refresh 前）
                ComputeStats();
                SchedulesView.Refresh();

                var products  = await _core.Data.GetAllProductsAsync();
                _allProducts  = products;
                RebuildFilteredProducts();

                var processes = await _core.Data.GetWorkProcessesAsync();
                _allProcesses = processes;
                RebuildFilteredProcesses();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 載入資料失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadAsync] {ex.Message}");
            }
        }

        private void ReplaceAll(IEnumerable<Schedule> schedules)
        {
            _all.Clear();
            _all.AddRange(schedules.Select(ScheduleUiModel.FromEntity));
        }

        // 載入倉位下拉來源（首項「(無)」+ 各倉位，帶現況箱數供顯示）。不擋停用/滿位，沿用 PR2「不啟用擋位」。
        private async Task LoadLocationChoicesAsync()
        {
            try
            {
                var locations = await _core.Warehouse.GetLocationsAsync();
                var occupancy = await _core.Warehouse.GetOccupancyCountsAsync();
                var choices = new List<PioLocationChoice> { new(null, null) };   // 首項＝(無) 不指派
                foreach (var loc in locations)
                {
                    occupancy.TryGetValue(loc.LocationId, out var count);
                    choices.Add(new PioLocationChoice(loc.LocationId, new StorageLocationRow(loc, count, null)));
                }
                var keepFormLocationId = FormLocationId;   // 防 SelectedValue 重建回寫 null（見 feedback_wpf_combobox_recompute_selection）
                LocationChoices = choices;
                FormLocationId = keepFormLocationId;        // 還原入料表單倉位選取
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 載入倉位清單失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadLocationChoices] {ex.Message}");
            }
        }

        // 依現役佔用對照設定各列 LocationId/LocationCode（供倉位欄三態顯示）；須在 SchedulesView.Refresh 前呼叫
        private async Task ApplyLocationsAsync()
        {
            try
            {
                var map = await _core.Warehouse.GetActiveAssignmentMapAsync();
                foreach (var s in _all)
                {
                    if (map.TryGetValue(s.ScheduleId, out var loc))
                    {
                        s.LocationId   = loc.LocationId;
                        s.LocationCode = loc.Code;
                    }
                    else
                    {
                        s.LocationId   = null;
                        s.LocationCode = null;
                    }
                }
            }
            catch (Exception ex)
            {
                // 倉位資訊缺失不阻斷排程清單顯示（箱體實體為真相來源）
                _core.Log.AddLog("[進出料管理] 載入倉位佔用失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[ApplyLocations] {ex.Message}");
            }
        }

        private bool IsWithinDateRange(ScheduleUiModel s)
        {
            if (!DateRangeStart.HasValue && !DateRangeEnd.HasValue) return true;
            if (!s.ReceivedAt.HasValue) return false;
            var businessDay = _core.Data.BusinessDay;
            var offset = businessDay == DateTime.MinValue ? TimeSpan.Zero : businessDay.TimeOfDay;
            if (DateRangeStart.HasValue && s.ReceivedAt.Value < DateRangeStart.Value.Date + offset)
                return false;
            if (DateRangeEnd.HasValue && s.ReceivedAt.Value >= DateRangeEnd.Value.Date.AddDays(1) + offset)
                return false;
            return true;
        }

        private IEnumerable<ScheduleUiModel> GetDateFilteredSource()
            => (!DateRangeStart.HasValue && !DateRangeEnd.HasValue) ? _all : _all.Where(IsWithinDateRange);

        private void ComputeStats()
        {
            var src = GetDateFilteredSource();
            StatTotal        = src.Count(s => s.Status != ScheduleStatus.Cancelled);
            StatTotalQty     = src.Where(s => s.Status != ScheduleStatus.Cancelled).Sum(s => s.Quantity);
            StatPending      = src.Count(s => s.Status == ScheduleStatus.Pending);
            StatPendingQty   = src.Where(s => s.Status == ScheduleStatus.Pending).Sum(s => s.Quantity);
            var pendingDevelop = src.Where(s => s.Status == ScheduleStatus.Pending && s.SopType == SopType.Develop).ToList();
            StatPendingDevelopCount = pendingDevelop.Count;
            StatPendingDevelopQty   = pendingDevelop.Sum(s => s.Quantity);
            StatScheduled    = src.Count(s => s.Status == ScheduleStatus.Scheduled);
            StatScheduledQty = src.Where(s => s.Status == ScheduleStatus.Scheduled).Sum(s => s.ActualQuantity ?? 0);
            StatCompleted    = src.Count(s => s.Status == ScheduleStatus.Completed);
            StatCompletedQty = src.Where(s => s.Status == ScheduleStatus.Completed).Sum(s => s.ActualQuantity ?? 0);
            StatReleased     = src.Count(s => s.Status == ScheduleStatus.Released);
            StatReleasedQty  = src.Where(s => s.Status == ScheduleStatus.Released).Sum(s => s.ActualQuantity ?? 0);
        }

        private void ApplyFilter() => SchedulesView?.Refresh();

        private bool FilterSchedule(object obj)
        {
            if (obj is not ScheduleUiModel s) return false;

            if (StatusFilter.HasValue && s.Status != StatusFilter.Value)
                return false;

            bool sopMatch = SopFilter switch
            {
                "New"     => s.SopType == null,
                "Develop" => s.SopType == SopType.Develop,
                "Mass"    => s.SopType != null && s.SopType != SopType.Develop,
                _         => true
            };
            if (!sopMatch) return false;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var kw = SearchText.Trim();
                if (!s.PartNo.Contains(kw, StringComparison.OrdinalIgnoreCase) &&
                    !s.BrandName.Contains(kw, StringComparison.OrdinalIgnoreCase) &&
                    !s.ModelName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(ProcessFilter))
            {
                var kw = ProcessFilter.Trim();
                if (!s.ProcessName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!IsWithinDateRange(s)) return false;

            return true;
        }

        private void RebuildFilteredProducts()
        {
            FilteredProducts.Clear();
            var results = _allProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FormPartFilter))
            {
                var kw = FormPartFilter.Trim();
                results = results.Where(p =>
                    p.Part?.PartNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (!string.IsNullOrWhiteSpace(FormModelFilter))
            {
                var kw = FormModelFilter.Trim();
                results = results.Where(p =>
                    p.Model?.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true);
            }

            foreach (var p in results) FilteredProducts.Add(p);
            SelectedProduct = FilteredProducts.FirstOrDefault();
        }

        private void RebuildFilteredProcesses()
        {
            FilteredProcesses.Clear();
            var results = _allProcesses.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(FormProcessFilter))
            {
                var kw = FormProcessFilter.Trim();
                results = results.Where(p =>
                    p.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true);
            }
            foreach (var p in results) FilteredProcesses.Add(p);
            SelectedProcess = FilteredProcesses.FirstOrDefault();
        }

        // ─── 日期快捷鍵 ──────────────────────────────────────────────────────────

        [RelayCommand]
        private void SetDateToday()
        {
            DateRangeStart = DateTime.Today;
            DateRangeEnd   = DateTime.Today;
        }

        [RelayCommand]
        private void SetDateLast3Days()
        {
            DateRangeStart = DateTime.Today.AddDays(-2);
            DateRangeEnd   = DateTime.Today;
        }

        [RelayCommand]
        private void SetDateLast5Days()
        {
            DateRangeStart = DateTime.Today.AddDays(-4);
            DateRangeEnd   = DateTime.Today;
        }

        [RelayCommand]
        private void SetDateCurrentWeek()
        {
            var today = DateTime.Today;
            var daysToMonday = ((int)today.DayOfWeek + 6) % 7;
            DateRangeStart = today.AddDays(-daysToMonday);
            DateRangeEnd   = DateRangeStart.Value.AddDays(6);
        }

        [RelayCommand]
        private void SetDateCurrentMonth()
        {
            var today = DateTime.Today;
            DateRangeStart = new DateTime(today.Year, today.Month, 1);
            DateRangeEnd   = DateRangeStart.Value.AddMonths(1).AddDays(-1);
        }

        // ─── 篩選面板切換 ────────────────────────────────────────────────────────

        [RelayCommand]
        private void ToggleFilterPanel() => IsFilterPanelVisible = !IsFilterPanelVisible;

        // ─── 面板控制 ─────────────────────────────────────────────────────────

        [RelayCommand]
        private void OpenDetail(ScheduleUiModel schedule)
        {
            SelectedSchedule = schedule;
            PanelMode = ProductInOutPanelMode.Detail;
            IsPanelVisible = true;
        }

        [RelayCommand]
        private void OpenForm()
        {
            ResetForm();
            PanelMode = ProductInOutPanelMode.Form;
            IsPanelVisible = true;
        }

        [RelayCommand]
        private void ClosePanel()
        {
            IsPanelVisible = false;
            SelectedSchedule = null;
        }

        private void ResetForm()
        {
            FormLotNo          = string.Empty;
            FormPartFilter     = string.Empty;
            FormModelFilter    = string.Empty;
            FormProcessFilter  = string.Empty;
            FormQuantity       = 0;
            FormDescription    = string.Empty;
            FormError          = null;
            FormLocationId     = null;   // 預設 (無) 不指派
            RebuildFilteredProducts();
            RebuildFilteredProcesses();
        }

        // ─── 入料表單提交 ──────────────────────────────────────────────────────

        [RelayCommand]
        private async Task SubmitForm()
        {
            if (SelectedProduct == null)  { FormError = Properties.Resources.PioFormErrorProduct; return; }
            if (SelectedProcess == null)  { FormError = Properties.Resources.PioFormErrorProcess; return; }
            if (FormQuantity <= 0)        { FormError = Properties.Resources.PioFormErrorQty;     return; }
            FormError = null;

            var userId = _core.Authorization.CurrentUser?.Id ?? 0;
            var dto = new ScheduleCreateDto
            {
                ProductId   = SelectedProduct.ProductId,
                ProcessId   = SelectedProcess.ProcessId,
                Quantity    = FormQuantity,
                LotNo       = string.IsNullOrWhiteSpace(FormLotNo) ? null : FormLotNo,
                Description = string.IsNullOrWhiteSpace(FormDescription) ? null : FormDescription,
                ReceivedBy  = userId
            };
            try
            {
                var newId = await _core.Data.AddScheduleAsync(dto);
                _core.Log.AddLog("[進出料管理] 入料成功");
                if (FormLocationId is int locId)
                {
                    // 選填倉位：入料後上架；上架失敗不阻斷入料（箱單已建立，倉位可事後補指派）
                    try
                    {
                        await _core.Warehouse.AssignAsync(locId, newId, userId);
                    }
                    catch (Exception wex)
                    {
                        _core.Log.AddLog("[進出料管理] 入料上架失敗，箱單已建立但未指派倉位", LogLevel.Error);
                        _core.Log.AddErrorLog($"[SubmitForm.Assign] {wex.Message}");
                    }
                }
                await ReloadAsync();
                ClosePanel();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 入料失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[SubmitForm] {ex.Message}");
                FormError = Properties.Resources.PioFormSubmitError;
            }
        }

        // ─── 操作按鈕 ─────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task ForceComplete(ScheduleUiModel schedule)
        {
            var userName = _core.Authorization.CurrentUser?.Name ?? string.Empty;
            var vm = new ScheduleOperationDialogViewModel(ScheduleOperationType.ForceComplete, schedule, userName);
            _dialog.ShowDialog(vm);
            if (!vm.IsConfirmed || vm.Result == null) return;
            var employeeId = _core.Authorization.CurrentUser?.Id ?? 0;
            try
            {
                await _core.Data.ForceCompleteAsync(schedule.ScheduleId, employeeId, vm.Result.ActualQuantity, vm.Result.Description ?? string.Empty);
                _core.Log.AddLog("[進出料管理] 強制完成成功");
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 強制完成失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[ForceComplete] {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Verify(ScheduleUiModel schedule)
        {
            List<OrderProductionInfo> orders;
            try
            {
                var entities = await _core.Data.GetOrdersByScheduleAsync(schedule.ScheduleId);
                orders = entities.Select(OrderProductionInfo.FromEntity).ToList();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 載入接單狀態失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[Verify] {ex.Message}");
                return;
            }

            var vm = new ScheduleOperationDialogViewModel(ScheduleOperationType.Verify, schedule, string.Empty, orders);
            _dialog.ShowDialog(vm);
            if (!vm.IsConfirmed || vm.Result == null) return;
            var employeeId = _core.Authorization.CurrentUser?.Id ?? 0;
            try
            {
                await _core.Data.MarkVerifiedAsync(schedule.ScheduleId, employeeId, vm.Result.ActualQuantity, vm.Result.Description);
                _core.Log.AddLog("[進出料管理] 確認完成成功");
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 確認完成失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[Verify] {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Release(ScheduleUiModel schedule)
        {
            var vm = new ScheduleOperationDialogViewModel(ScheduleOperationType.Release, schedule, string.Empty);
            _dialog.ShowDialog(vm);
            if (!vm.IsConfirmed || vm.Result == null) return;
            var employeeId = _core.Authorization.CurrentUser?.Id ?? 0;
            try
            {
                await _core.Data.MarkReleasedAsync(schedule.ScheduleId, employeeId, vm.Result.Description);
                _core.Log.AddLog("[進出料管理] 出料成功");
                await ReleaseLocationIfOccupiedAsync(schedule, employeeId);   // 出料連動釋放倉位
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 出料失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[Release] {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Split(ScheduleUiModel schedule)
        {
            var vm = new ScheduleOperationDialogViewModel(ScheduleOperationType.Split, schedule, string.Empty);
            _dialog.ShowDialog(vm);
            if (!vm.IsConfirmed || vm.Result == null) return;
            var employeeId = _core.Authorization.CurrentUser?.Id ?? 0;
            try
            {
                var splitDto = new ScheduleSplitDto
                {
                    OriginalScheduleId = schedule.ScheduleId,
                    RemainingQuantity  = vm.Result.RemainingQuantity ?? 0,
                    ReleasedBy         = employeeId,
                    Description        = vm.Result.Description
                };
                var childId = await _core.Data.SplitScheduleAsync(splitDto);
                _core.Log.AddLog("[進出料管理] 拆單成功");
                if (schedule.LocationId is int origLocId)
                {
                    // 原單釋放原倉位、子單沿用原倉位（跨域非原子，失敗記 log 不回滾）
                    try
                    {
                        await _core.Warehouse.ReleaseAsync(schedule.ScheduleId, employeeId);
                        await _core.Warehouse.AssignAsync(origLocId, childId, employeeId);
                    }
                    catch (Exception wex)
                    {
                        _core.Log.AddLog("[進出料管理] 拆單倉位轉移失敗，請手動確認倉位", LogLevel.Error);
                        _core.Log.AddErrorLog($"[Split.Location] {wex.Message}");
                    }
                }
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 拆單失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[Split] {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CancelSchedule(ScheduleUiModel schedule)
        {
            var vm = new ScheduleOperationDialogViewModel(ScheduleOperationType.Cancel, schedule, string.Empty);
            _dialog.ShowDialog(vm);
            if (!vm.IsConfirmed || vm.Result == null) return;
            var employeeId = _core.Authorization.CurrentUser?.Id ?? 0;
            try
            {
                await _core.Data.CancelScheduleAsync(schedule.ScheduleId, vm.Result.Description);
                _core.Log.AddLog("[進出料管理] 取消成功");
                await ReleaseLocationIfOccupiedAsync(schedule, employeeId);   // 取消連動釋放倉位
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 取消失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[CancelSchedule] {ex.Message}");
            }
        }

        // ─── 倉位指派 / 改倉 / 取消指派 ───────────────────────────────────────────

        [RelayCommand]
        private async Task AssignLocation(ScheduleUiModel schedule)
        {
            var vm = new AssignLocationDialogViewModel(schedule, LocationChoices);
            _dialog.ShowDialog(vm);
            if (!vm.IsConfirmed || vm.Result is null) return;

            var operatorId = _core.Authorization.CurrentUser?.Id ?? 0;
            var newLocId = vm.Result.LocationId;   // null = 選了「(無)」= 取消指派
            var curLocId = schedule.LocationId;
            if (newLocId == curLocId) return;      // no-op（含 both null）

            try
            {
                if (curLocId == null)
                    await _core.Warehouse.AssignAsync(newLocId!.Value, schedule.ScheduleId, operatorId);   // 未指派 → 指派
                else if (newLocId == null)
                    await _core.Warehouse.ReleaseAsync(schedule.ScheduleId, operatorId);                   // 有倉 → (無) 取消指派
                else
                    await _core.Warehouse.ReassignAsync(schedule.ScheduleId, newLocId.Value, operatorId);  // 有倉 → 改倉（原子）
                _core.Log.AddLog("[進出料管理] 倉位更新成功");
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 倉位更新失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[AssignLocation] {ex.Message}");
                await ReloadAsync();   // 還原顯示至實際狀態
            }
        }

        // 主業務（出料/取消）成功後釋放現役倉位；未指派箱（LocationCode==null）跳過以避開 repo throw
        private async Task ReleaseLocationIfOccupiedAsync(ScheduleUiModel schedule, int operatorId)
        {
            if (schedule.LocationCode == null) return;
            try
            {
                await _core.Warehouse.ReleaseAsync(schedule.ScheduleId, operatorId);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 倉位釋放失敗，請手動確認倉位", LogLevel.Error);
                _core.Log.AddErrorLog($"[ReleaseLocation] {ex.Message}");
            }
        }

        private async Task ReloadAsync()
        {
            var schedules = await _core.Data.GetAllSchedulesAsync();
            ReplaceAll(schedules);
            await LoadLocationChoicesAsync();   // 重查佔用、重建下拉，刷新 (箱數/容量)
            await ApplyLocationsAsync();        // 重新套用各列倉位（須在 Refresh 前）
            ComputeStats();
            SchedulesView.Refresh();
            if (SelectedSchedule != null)
            {
                var updated = _all.FirstOrDefault(s => s.ScheduleId == SelectedSchedule.ScheduleId);
                SelectedSchedule = updated;
                if (updated == null) IsPanelVisible = false;
            }
        }
    }

    public sealed record ScheduleStatusFilterOption(ScheduleStatus? Value);
    public sealed record PioSopFilterOption(string Value);

    /// <summary>倉位下拉/改倉 Dialog 的選項：LocationId 為 null 代表「(無)」不指派；Row 為 null 即 (無) 哨兵項。</summary>
    public sealed record PioLocationChoice(int? LocationId, StorageLocationRow? Row)
    {
        public bool IsNone => Row == null;
    }
}
