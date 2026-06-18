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

        private List<Product> _allProducts = new();
        public ObservableCollection<Product> FilteredProducts { get; } = new();
        [ObservableProperty] private Product? selectedProduct;

        private List<WorkProcess> _allProcesses = new();
        public ObservableCollection<WorkProcess> FilteredProcesses { get; } = new();
        [ObservableProperty] private WorkProcess? selectedProcess;

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
                await _core.Data.AddScheduleAsync(dto);
                _core.Log.AddLog("[進出料管理] 入料成功");
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
            var vm = new ScheduleOperationDialogViewModel(ScheduleOperationType.Verify, schedule, string.Empty);
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
                await _core.Data.SplitScheduleAsync(splitDto);
                _core.Log.AddLog("[進出料管理] 拆單成功");
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
            try
            {
                await _core.Data.CancelScheduleAsync(schedule.ScheduleId, vm.Result.Description);
                _core.Log.AddLog("[進出料管理] 取消成功");
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 取消失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[CancelSchedule] {ex.Message}");
            }
        }

        private async Task ReloadAsync()
        {
            var schedules = await _core.Data.GetAllSchedulesAsync();
            ReplaceAll(schedules);
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
}
