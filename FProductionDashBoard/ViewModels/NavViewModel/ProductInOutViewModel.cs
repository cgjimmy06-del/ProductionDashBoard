using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public enum ProductInOutPanelMode { Detail, Form }

    public partial class ProductInOutViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
        private readonly IDialogService _dialog;
        private List<ScheduleUiModel> _all = new();

        // 統計
        [ObservableProperty] private int statPending;
        [ObservableProperty] private int statScheduled;
        [ObservableProperty] private int statCompleted;
        [ObservableProperty] private int statReleased;

        // 篩選
        [ObservableProperty] private ScheduleStatus? statusFilter;
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private string processFilter = string.Empty;

        public ObservableCollection<ScheduleUiModel> Schedules { get; } = new();

        // 右側面板
        [ObservableProperty] private bool isPanelVisible;
        [ObservableProperty] private ProductInOutPanelMode panelMode;
        [ObservableProperty] private ScheduleUiModel? selectedSchedule;

        // 入料表單
        [ObservableProperty] private string formLotNo = string.Empty;
        [ObservableProperty] private string formPartFilter = string.Empty;
        [ObservableProperty] private string formModelFilter = string.Empty;
        [ObservableProperty] private int formQuantity;
        [ObservableProperty] private string formDescription = string.Empty;
        [ObservableProperty] private string? formError;

        private List<Product> _allProducts = new();
        public ObservableCollection<Product> FilteredProducts { get; } = new();
        [ObservableProperty] private Product? selectedProduct;

        public ObservableCollection<WorkProcess> Processes { get; } = new();
        [ObservableProperty] private WorkProcess? selectedProcess;

        public IReadOnlyList<ScheduleStatusFilterOption> StatusFilterOptions { get; }

        partial void OnStatusFilterChanged(ScheduleStatus? value) => ApplyFilter();
        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnProcessFilterChanged(string value) => ApplyFilter();
        partial void OnFormPartFilterChanged(string value) => RebuildFilteredProducts();
        partial void OnFormModelFilterChanged(string value) => RebuildFilteredProducts();

        public ProductInOutViewModel(DashboardCoreServices core, IDialogService dialog)
        {
            _core = core;
            _dialog = dialog;
            StatusFilterOptions = new ScheduleStatusFilterOption[] { new(null) }
                .Concat(Enum.GetValues<ScheduleStatus>().Select(s => new ScheduleStatusFilterOption(s)))
                .ToArray();
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var schedules = await _core.Data.GetAllSchedulesAsync();
                _all = schedules.Select(ScheduleUiModel.FromEntity).ToList();
                ComputeStats();
                ApplyFilter();

                var products  = await _core.Data.GetAllProductsAsync();
                _allProducts  = products;
                RebuildFilteredProducts();

                var processes = await _core.Data.GetWorkProcessesAsync();
                Processes.Clear();
                foreach (var p in processes) Processes.Add(p);
                SelectedProcess = Processes.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[進出料管理] 載入資料失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadAsync] {ex.Message}");
            }
        }

        private void ComputeStats()
        {
            StatPending   = _all.Count(s => s.Status == ScheduleStatus.Pending);
            StatScheduled = _all.Count(s => s.Status == ScheduleStatus.Scheduled);
            StatCompleted = _all.Count(s => s.Status == ScheduleStatus.Completed);
            StatReleased  = _all.Count(s => s.Status == ScheduleStatus.Released);
        }

        private void ApplyFilter()
        {
            Schedules.Clear();
            var filtered = _all.AsEnumerable();

            if (StatusFilter.HasValue)
                filtered = filtered.Where(s => s.Status == StatusFilter.Value);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var kw = SearchText.Trim();
                filtered = filtered.Where(s =>
                    s.PartNo.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    s.BrandName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    s.ModelName.Contains(kw, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(ProcessFilter))
            {
                var kw = ProcessFilter.Trim();
                filtered = filtered.Where(s =>
                    s.ProcessName.Contains(kw, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var s in filtered.OrderBy(s => s.ScheduleId))
                Schedules.Add(s);
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
            FormLotNo       = string.Empty;
            FormPartFilter  = string.Empty;
            FormModelFilter = string.Empty;
            FormQuantity    = 0;
            FormDescription = string.Empty;
            FormError       = null;
            RebuildFilteredProducts();
            SelectedProcess = Processes.FirstOrDefault();
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
                var schedules = await _core.Data.GetAllSchedulesAsync();
                _all = schedules.Select(ScheduleUiModel.FromEntity).ToList();
                ComputeStats();
                ApplyFilter();
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
            _all = schedules.Select(ScheduleUiModel.FromEntity).ToList();
            ComputeStats();
            ApplyFilter();
            if (SelectedSchedule != null)
            {
                var updated = _all.FirstOrDefault(s => s.ScheduleId == SelectedSchedule.ScheduleId);
                SelectedSchedule = updated;
                if (updated == null) IsPanelVisible = false;
            }
        }
    }

    public sealed record ScheduleStatusFilterOption(ScheduleStatus? Value);
}
