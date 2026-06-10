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

    public sealed record ScheduleViewFilterOption(string Label, ScheduleViewFilter Value);

    public partial class ScheduleViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
        private readonly List<ScheduleUiModel> _allSchedules = new();
        private readonly List<OrderProductionInfo> _allOrders = new();

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

        public ICollectionView SchedulesView { get; }
        public IReadOnlyList<ScheduleViewFilterOption> StatusFilterOptions { get; }

        partial void OnStatusFilterChanged(ScheduleViewFilter value) => ApplyFilter();
        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnProcessFilterChanged(string value) => ApplyFilter();

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

        public ScheduleViewModel(DashboardCoreServices core)
        {
            _core = core;

            StatusFilterOptions = new ScheduleViewFilterOption[]
            {
                new("全部（進行中）",  ScheduleViewFilter.AllActive),
                new("待排單",         ScheduleViewFilter.Pending),
                new("已排單",         ScheduleViewFilter.Scheduled),
                new("生產完成",       ScheduleViewFilter.ProductionDone),
                new("──────────",    ScheduleViewFilter.Separator),
                new("已驗收",         ScheduleViewFilter.Completed),
                new("已出料",         ScheduleViewFilter.Released),
                new("已取消",         ScheduleViewFilter.Cancelled),
            };

            DateRangeStart = DateTime.Today.AddDays(-30);
            DateRangeEnd   = DateTime.Today;

            SchedulesView = CollectionViewSource.GetDefaultView(_allSchedules);
            SchedulesView.Filter = FilterSchedule;
            SchedulesView.SortDescriptions.Add(
                new SortDescription(nameof(ScheduleUiModel.ScheduleId), ListSortDirection.Descending));

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
        }

        private async Task LoadAllAsync()
        {
            try
            {
                var schedules = await _core.Data.GetAllSchedulesAsync().ConfigureAwait(false);
                var orders    = await _core.Data.GetAllOrderProductionsAsync().ConfigureAwait(false);

                _allSchedules.Clear();
                _allSchedules.AddRange(schedules.Select(ScheduleUiModel.FromEntity));

                _allOrders.Clear();
                _allOrders.AddRange(orders.Select(OrderProductionInfo.FromEntity));

                ComputeBadges();
                ComputeStats();
                SchedulesView.Refresh();
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
    }
}
