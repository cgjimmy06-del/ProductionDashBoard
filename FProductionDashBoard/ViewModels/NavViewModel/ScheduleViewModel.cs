using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FProductionDashBoard.ViewModels
{
    public enum ScheduleViewFilter
    {
        AllActive,
        Pending,
        Scheduled,
        ProductionDone,
        Separator,
        Completed,
        Released,
        Cancelled
    }

    public sealed record ScheduleViewFilterOption(ScheduleViewFilter Value);

    public partial class ScheduleViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
        private readonly List<ScheduleUiModel> _allSchedules = new();
        private readonly List<OrderProductionInfo> _allOrders = new();
        private readonly List<ScheduleEquipmentCardViewModel> _allCards = new();
        private HashSet<(int ProductId, int ProcessId)> _visibleScheduleKeys = new();

        // 統計
        [ObservableProperty] private int statTotal;
        [ObservableProperty] private int statTotalQty;
        [ObservableProperty] private int statPending;
        [ObservableProperty] private int statPendingQty;
        [ObservableProperty] private int statScheduled;
        [ObservableProperty] private int statScheduledActualQty;
        [ObservableProperty] private int statProductionDone;
        [ObservableProperty] private int statProductionDoneActualQty;
        [ObservableProperty] private int statUnderScheduledCount;
        [ObservableProperty] private int statUnderScheduledQtyDeficit;

        // 篩選
        [ObservableProperty] private ScheduleViewFilter statusFilter = ScheduleViewFilter.AllActive;
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private string processFilter = string.Empty;
        [ObservableProperty] private DateTime? dateRangeStart;
        [ObservableProperty] private DateTime? dateRangeEnd;

        // Layer 2：選取排單
        [ObservableProperty] private ScheduleUiModel? selectedSchedule;

        // 設備統計卡片（全體設備）
        [ObservableProperty] private int statIdleCardCount;
        [ObservableProperty] private int statLowLoadCardCount;

        // 右側統計列（Layer 2 才有意義）
        [ObservableProperty] private int selectedScheduleOrderTotal;
        [ObservableProperty] private int selectedScheduleOrderIncomplete;
        [ObservableProperty] private string selectedScheduleEquipments = string.Empty;
        public bool IsStatsPanelVisible => SelectedSchedule != null;

        public ICollectionView SchedulesView { get; }
        public ICollectionView EquipmentCardsView { get; }
        public IReadOnlyList<ScheduleViewFilterOption> StatusFilterOptions { get; }

        partial void OnStatusFilterChanged(ScheduleViewFilter value) => ApplyFilter();
        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnProcessFilterChanged(string value) => ApplyFilter();

        partial void OnSelectedScheduleChanged(ScheduleUiModel? value)
        {
            if (value == null)
                foreach (var c in _allCards) c.ResetForLayer1();
            else
                UpdateCardCompatibility(value);

            EquipmentCardsView?.Refresh();
            UpdateSelectedScheduleStats(value);
            OnPropertyChanged(nameof(IsStatsPanelVisible));
        }

        partial void OnDateRangeStartChanged(DateTime? value)
        {
            if (value.HasValue && (DateRangeEnd == null || DateRangeEnd < value))
                DateRangeEnd = value;
            ComputeStats();
            SchedulesView?.Refresh();
            UpdateVisibleScheduleKeys();
            EquipmentCardsView?.Refresh();
        }

        partial void OnDateRangeEndChanged(DateTime? value)
        {
            ComputeStats();
            SchedulesView?.Refresh();
            UpdateVisibleScheduleKeys();
            EquipmentCardsView?.Refresh();
        }

        public ScheduleViewModel(DashboardCoreServices core)
        {
            _core = core;

            StatusFilterOptions = new ScheduleViewFilterOption[]
            {
                new(ScheduleViewFilter.AllActive),
                new(ScheduleViewFilter.Pending),
                new(ScheduleViewFilter.Scheduled),
                new(ScheduleViewFilter.ProductionDone),
                new(ScheduleViewFilter.Separator),
                new(ScheduleViewFilter.Completed),
                new(ScheduleViewFilter.Released),
                new(ScheduleViewFilter.Cancelled),
            };

            DateRangeStart = DateTime.Today.AddDays(-30);
            DateRangeEnd   = DateTime.Today;

            SchedulesView = CollectionViewSource.GetDefaultView(_allSchedules);
            SchedulesView.Filter = FilterSchedule;
            SchedulesView.SortDescriptions.Add(
                new SortDescription(nameof(ScheduleUiModel.ScheduleId), ListSortDirection.Descending));

            EquipmentCardsView = CollectionViewSource.GetDefaultView(_allCards);
            EquipmentCardsView.Filter = FilterCard;
            EquipmentCardsView.SortDescriptions.Add(
                new SortDescription(nameof(ScheduleEquipmentCardViewModel.SortOrder), ListSortDirection.Ascending));
            EquipmentCardsView.SortDescriptions.Add(
                new SortDescription(nameof(ScheduleEquipmentCardViewModel.Name), ListSortDirection.Ascending));

            _ = LoadAllAsync();
        }

        public void InjectDataForTest(List<ScheduleUiModel> schedules, List<OrderProductionInfo> orders)
        {
            _allSchedules.Clear();
            _allSchedules.AddRange(schedules);
            _allOrders.Clear();
            _allOrders.AddRange(orders);
            ComputeBadges();
            ComputeStats();
            SchedulesView.Refresh();
            UpdateVisibleScheduleKeys();
            EquipmentCardsView.Refresh();
        }

        public void InjectCardsForTest(List<EquipmentProduct> equipmentProducts, List<ProgramTuningRecord>? tunings = null)
        {
            BuildEquipmentCards(equipmentProducts, tunings ?? new());
            ComputeCardStats();
            UpdateVisibleScheduleKeys();
            EquipmentCardsView.Refresh();
        }

        private async Task LoadAllAsync()
        {
            try
            {
                var schedules    = await _core.Data.GetAllSchedulesAsync();
                var orders       = await _core.Data.GetAllOrderProductionsAsync();
                var allEps       = await _core.Data.GetAllEquipmentProductsAsync();
                var inProgressTunings = await _core.Data.GetAllInProgressProgramTuningAsync();

                _allSchedules.Clear();
                _allSchedules.AddRange(schedules.Select(ScheduleUiModel.FromEntity));

                _allOrders.Clear();
                _allOrders.AddRange(orders.Select(OrderProductionInfo.FromEntity));

                ComputeBadges();
                ComputeStats();

                BuildEquipmentCards(allEps, inProgressTunings);
                ComputeCardStats();

                SchedulesView.Refresh();
                UpdateVisibleScheduleKeys();
                EquipmentCardsView.Refresh();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[排單管理] 載入資料失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadAllAsync] {ex.Message}");
            }
        }

        private void ComputeBadges()
        {
            var today = DateTime.Today;
            foreach (var s in _allSchedules)
            {
                s.DerivedBadge = GetDerivedBadge(s.ScheduleId);

                if (s.Status == ScheduleStatus.Pending && s.ReceivedAt.HasValue)
                {
                    var days = (today - s.ReceivedAt.Value.Date).Days;
                    s.WaitingDaysText = days switch
                    {
                        0          => null,
                        <= 5       => $"已等待 {days} 天",
                        _          => $"⚠ 已等待 {days} 天"
                    };
                }
                else
                {
                    s.WaitingDaysText = null;
                }
            }
        }

        private string? GetDerivedBadge(int scheduleId)
        {
            var orders = _allOrders.Where(o => o.ScheduleId == scheduleId).ToList();
            if (!orders.Any()) return null;

            if (orders.Any(o => o.Status == OrderProductionStatus.InProduction))
                return "⬟ 生產中";

            var nonCancelled = orders.Where(o => o.Status != OrderProductionStatus.Cancelled).ToList();
            if (!nonCancelled.Any())
                return "⚠ 取消注意";

            if (nonCancelled.All(o => o.Status == OrderProductionStatus.Completed))
            {
                var schedule = _allSchedules.FirstOrDefault(s => s.ScheduleId == scheduleId);
                if (schedule != null)
                {
                    var actualQty = nonCancelled.Sum(o => o.Quantity ?? 0);
                    return actualQty >= schedule.Quantity ? "✓ 生產完成" : "⚠ 部分完成";
                }
            }

            return null;
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
            => (!DateRangeStart.HasValue && !DateRangeEnd.HasValue) ? _allSchedules : _allSchedules.Where(IsWithinDateRange);

        private void ComputeStats()
        {
            var src = GetDateFilteredSource().ToList();

            StatTotal    = src.Count(s => s.Status != ScheduleStatus.Cancelled);
            StatTotalQty = src.Where(s => s.Status != ScheduleStatus.Cancelled).Sum(s => s.Quantity);

            var pending = src.Where(s => s.Status == ScheduleStatus.Pending).ToList();
            StatPending    = pending.Count;
            StatPendingQty = pending.Sum(s => s.Quantity);

            var scheduled = src.Where(s => s.Status == ScheduleStatus.Scheduled).ToList();
            StatScheduled          = scheduled.Count;
            StatScheduledActualQty = scheduled.Sum(s => s.ActualQuantity ?? 0);

            var underScheduled = scheduled.Where(s => s.ActualQuantity < s.Quantity).ToList();
            StatUnderScheduledCount      = underScheduled.Count;
            StatUnderScheduledQtyDeficit = underScheduled.Sum(s => s.Quantity - (s.ActualQuantity ?? 0));

            var productionDone = src.Where(s =>
                s.Status == ScheduleStatus.Scheduled &&
                s.DerivedBadge == "✓ 生產完成").ToList();
            StatProductionDone          = productionDone.Count;
            StatProductionDoneActualQty = productionDone.Sum(s => s.ActualQuantity ?? 0);
        }

        private void ApplyFilter()
        {
            SchedulesView?.Refresh();
            UpdateVisibleScheduleKeys();
            EquipmentCardsView?.Refresh();
            ComputeStats();
        }

        private bool FilterSchedule(object obj)
        {
            if (obj is not ScheduleUiModel s) return false;

            switch (StatusFilter)
            {
                case ScheduleViewFilter.AllActive:
                    if (s.Status != ScheduleStatus.Pending && s.Status != ScheduleStatus.Scheduled)
                        return false;
                    break;
                case ScheduleViewFilter.Pending:
                    if (s.Status != ScheduleStatus.Pending) return false;
                    break;
                case ScheduleViewFilter.Scheduled:
                    if (s.Status != ScheduleStatus.Scheduled) return false;
                    break;
                case ScheduleViewFilter.ProductionDone:
                    if (s.Status != ScheduleStatus.Scheduled || s.DerivedBadge != "✓ 生產完成")
                        return false;
                    break;
                case ScheduleViewFilter.Completed:
                    if (s.Status != ScheduleStatus.Completed) return false;
                    break;
                case ScheduleViewFilter.Released:
                    if (s.Status != ScheduleStatus.Released) return false;
                    break;
                case ScheduleViewFilter.Cancelled:
                    if (s.Status != ScheduleStatus.Cancelled) return false;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var kw = SearchText.Trim();
                if (!(s.PartNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
                      s.ModelName?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
                      s.LotNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(ProcessFilter))
            {
                var kw = ProcessFilter.Trim();
                if (s.ProcessName?.Contains(kw, StringComparison.OrdinalIgnoreCase) != true)
                    return false;
            }

            if (!IsWithinDateRange(s)) return false;

            return true;
        }

        // ─── 重新整理 ─────────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task Refresh()
        {
            try
            {
                await LoadAllAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[排單管理] 重新整理失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[Refresh] {ex.Message}");
            }
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

        // ─── 設備卡片牆 ───────────────────────────────────────────────────────────

        private bool FilterCard(object obj)
        {
            if (obj is not ScheduleEquipmentCardViewModel card) return false;

            if (SelectedSchedule != null)
                return card.HasMatchingProgram;

            if (_visibleScheduleKeys.Any())
                return card.HasAnyFeasibleEpForKeys(_visibleScheduleKeys);

            return true;
        }

        private void UpdateVisibleScheduleKeys()
        {
            _visibleScheduleKeys = _allSchedules
                .Where(FilterSchedule)
                .Select(s => (s.ProductId, s.ProcessId))
                .ToHashSet();
        }

        private void BuildEquipmentCards(List<EquipmentProduct> allEps, List<ProgramTuningRecord> inProgressTunings)
        {
            _allCards.Clear();

            foreach (var group in allEps.GroupBy(ep => ep.EquipmentId))
            {
                var eq = group.First().Equipment;
                if (eq == null) continue;

                var equipOrders   = _allOrders.Where(o => o.EquipmentId == eq.Id).ToList();
                var pendingOrders = equipOrders.Where(o => o.Status == OrderProductionStatus.Pending).ToList();
                var inProdOrders  = equipOrders.Where(o => o.Status == OrderProductionStatus.InProduction).ToList();

                var card = new ScheduleEquipmentCardViewModel
                {
                    EquipmentId          = eq.Id,
                    Code                 = eq.Code,
                    Name                 = eq.Name,
                    AllEquipmentProducts = group.ToList(),
                };
                card.PendingOrderCount    = pendingOrders.Count;
                card.InProductionOrderCount = inProdOrders.Count;
                card.TotalPendingQty      = pendingOrders.Sum(o => o.Quantity ?? 0);
                card.InProductionInfo     = inProdOrders.FirstOrDefault()?.ProductName;
                card.ActiveTuning         = inProgressTunings.FirstOrDefault(t => t.EquipmentId == eq.Id);

                _allCards.Add(card);
            }
        }

        private void ComputeCardStats()
        {
            StatIdleCardCount    = _allCards.Count(c => c.HasAnyFeasibleEp &&
                                                        c.PendingOrderCount == 0 &&
                                                        c.InProductionOrderCount == 0);
            StatLowLoadCardCount = _allCards.Count(c => c.LoadLevel == ScheduleCardLoadLevel.Low);
        }

        private void UpdateCardCompatibility(ScheduleUiModel schedule)
        {
            foreach (var card in _allCards)
            {
                var matching = card.AllEquipmentProducts
                    .Where(ep => ep.Sop?.ProductId == schedule.ProductId && ep.Sop?.ProcessId == schedule.ProcessId)
                    .ToList();

                card.HasMatchingProgram = matching.Any();

                var feasible = matching.Where(ep => ep.ProductionStatus == TuningType.Feasible).ToList();
                card.IsCompatibleWithSelectedSchedule = feasible.Any();
                card.CompatibleEquipmentProducts = feasible;
                card.FeasibleMatchingCount = feasible.Count;
                card.TotalMatchingCount    = matching.Count;
            }
        }

        private void UpdateSelectedScheduleStats(ScheduleUiModel? schedule)
        {
            if (schedule == null)
            {
                SelectedScheduleOrderTotal      = 0;
                SelectedScheduleOrderIncomplete = 0;
                SelectedScheduleEquipments      = string.Empty;
                return;
            }

            var orders = _allOrders.Where(o => o.ScheduleId == schedule.ScheduleId).ToList();
            SelectedScheduleOrderTotal = orders.Count(o => o.Status != OrderProductionStatus.Cancelled);
            SelectedScheduleOrderIncomplete = orders.Count(o =>
                o.Status == OrderProductionStatus.Pending || o.Status == OrderProductionStatus.InProduction);

            var assignedIds = orders
                .Where(o => o.Status != OrderProductionStatus.Cancelled)
                .Select(o => o.EquipmentId)
                .ToHashSet();
            SelectedScheduleEquipments = string.Join(", ", _allCards
                .Where(c => assignedIds.Contains(c.EquipmentId))
                .Select(c => c.Name));
        }
    }
}
