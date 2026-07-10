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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FProductionDashBoard.ViewModels
{
    /// <summary>
    /// 圖表頁：依 ChartDefinition 宣告式定義渲染統計列與容器（卡片牆/表格）。
    /// 來源資料於 LoadAsync 一次載入快取，頁籤切換僅在記憶體重建列，不重打 DB。
    /// </summary>
    public partial class ChartViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
        private readonly IChartDefinitionStore _store;

        private List<Schedule> _schedules = new();
        private List<OrderProductionInfo> _orders = new();
        private List<EquipmentProduct> _allEps = new();
        private List<ProgramTuningRecord> _tunings = new();

        private readonly ObservableCollection<ChartRowViewModel> _rows = new();

        public ObservableCollection<ChartTabItemViewModel> Tabs { get; } = new();
        public ObservableCollection<ChartStatItemViewModel> StatItems { get; } = new();
        public ICollectionView RowsView { get; }

        [ObservableProperty] private ChartTabItemViewModel? selectedTab;
        [ObservableProperty] private bool isStatRowVisible;
        [ObservableProperty] private bool isCardContainer;
        [ObservableProperty] private bool isTableContainer;
        [ObservableProperty] private string? indicatorFieldId;
        [ObservableProperty] private IReadOnlyList<ChartTableColumn> tableColumns = Array.Empty<ChartTableColumn>();

        // 篩選列狀態（依定義於 BuildFilterRow 重建）
        public ObservableCollection<ChartFilterFieldViewModel> FilterFields { get; } = new();
        public ObservableCollection<ChartQuickButtonViewModel> QuickButtons { get; } = new();
        public ObservableCollection<ChartSortOptionItem> SortOptions { get; } = new();

        [ObservableProperty] private bool isFilterRowVisible;
        [ObservableProperty] private ChartSortOptionItem? selectedSortOption;
        [ObservableProperty] private bool isSortDescending;
        [ObservableProperty] private int zoomPercent = ChartConstants.DefaultZoomPercent;

        /// <summary>檢視端縮放倍率（內容區單一 LayoutTransform 用）</summary>
        public double ZoomScale => ZoomPercent / 100.0;

        /// <summary>重建/恢復預設期間抑制逐項刷新，結束後一次套用</summary>
        private bool _suppressRefresh;

        public ChartViewModel(DashboardCoreServices core, IChartDefinitionStore store)
        {
            _core = core;
            _store = store;
            RowsView = CollectionViewSource.GetDefaultView(_rows);
            RowsView.Filter = FilterRow;

            // 縮放偏好屬本機不進定義；直接設欄位避免觸發 OnZoomPercentChanged 回存
            var savedZoom = Properties.Settings.Default.ChartZoomPercent;
            zoomPercent = ChartConstants.ZoomLevels.Contains(savedZoom)
                ? savedZoom
                : ChartConstants.DefaultZoomPercent;
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            try
            {
                var schedules = await _core.Data.GetAllSchedulesAsync();
                var orders    = await _core.Data.GetAllOrderProductionsAsync();
                var allEps    = await _core.Data.GetAllEquipmentProductsAsync();
                var tunings   = await _core.Data.GetAllInProgressProgramTuningAsync();

                ApplyData(schedules, orders.Select(OrderProductionInfo.FromEntity).ToList(), allEps, tunings);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[圖表] 載入資料失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadAsync] {ex.Message}");
            }
        }

        /// <summary>測試注入：繞過 IDataService 與 WPF Dispatcher（比照 ScheduleViewModel.InjectDataForTest）</summary>
        internal void InjectDataForTest(List<Schedule> schedules, List<OrderProductionInfo> orders,
            List<EquipmentProduct> allEps, List<ProgramTuningRecord> tunings)
            => ApplyData(schedules, orders, allEps, tunings);

        private void ApplyData(List<Schedule> schedules, List<OrderProductionInfo> orders,
            List<EquipmentProduct> allEps, List<ProgramTuningRecord> tunings)
        {
            _schedules = schedules;
            _orders    = orders;
            _allEps    = allEps;
            _tunings   = tunings;
            ReloadTabs();
        }

        private void ReloadTabs()
        {
            var previousId = SelectedTab?.Definition.Id;

            Tabs.Clear();
            foreach (var def in _store.Load())
                Tabs.Add(new ChartTabItemViewModel(def));

            var restored = Tabs.FirstOrDefault(t => t.Definition.Id == previousId) ?? Tabs.FirstOrDefault();
            if (ReferenceEquals(SelectedTab, restored))
                RebuildForDefinition();     // setter 不觸發時（皆為 null）仍需重建清空
            else
                SelectedTab = restored;     // setter 觸發 OnSelectedTabChanged → 重建
        }

        partial void OnSelectedTabChanged(ChartTabItemViewModel? value) => RebuildForDefinition();

        private void RebuildForDefinition()
        {
            var def = SelectedTab?.Definition;

            _rows.Clear();
            StatItems.Clear();

            if (def == null)
            {
                IsStatRowVisible = false;
                IsCardContainer  = false;
                IsTableContainer = false;
                IndicatorFieldId = null;
                TableColumns = Array.Empty<ChartTableColumn>();
                ClearFilterRow();
                IsFilterRowVisible = false;
                return;
            }

            var rows = def.DataSet == ChartDataSet.Equipment
                ? BuildEquipmentRows(def)
                : BuildScheduleRows(def);
            foreach (var row in rows)
                _rows.Add(row);

            ComputeStats(def);
            ApplyContainer(def);
            BuildFilterRow(def);
        }

        #region 列建構（衍生欄位計算）

        private List<ChartRowViewModel> BuildEquipmentRows(ChartDefinition def)
        {
            var rows = new List<ChartRowViewModel>();

            foreach (var group in _allEps.GroupBy(ep => ep.EquipmentId))
            {
                var eq = group.First().Equipment;
                if (eq == null) continue;

                var equipOrders  = _orders.Where(o => o.EquipmentId == eq.Id).ToList();
                var pendingCount = equipOrders.Count(o => o.Status == OrderProductionStatus.Pending);
                var inProdOrders = equipOrders.Where(o => o.Status == OrderProductionStatus.InProduction).ToList();
                var totalPendingQty = equipOrders
                    .Where(o => o.Status is OrderProductionStatus.Pending or OrderProductionStatus.InProduction)
                    .Sum(o => o.Quantity ?? 0);
                var anyFeasible = group.Any(ep => ep.ProductionStatus == TuningType.Feasible);

                // 生產狀態優先序比照 ScheduleEquipmentCardViewModel.CardColor：無可生產 > 生產中 > 待生產 > 閒置
                var status = !anyFeasible           ? FieldCatalog.ValUnavailable :
                             inProdOrders.Count > 0 ? FieldCatalog.ValInProduction :
                             pendingCount > 0       ? FieldCatalog.ValPendingProd :
                                                      FieldCatalog.ValIdle;

                // 負載門檻比照 ScheduleEquipmentCardViewModel.LoadLevel
                var load = totalPendingQty == 0   ? FieldCatalog.ValLoadNone :
                           totalPendingQty < 100  ? FieldCatalog.ValLoadLow :
                           totalPendingQty <= 500 ? FieldCatalog.ValLoadMid :
                                                    FieldCatalog.ValLoadHigh;

                var tuning = _tunings.Any(t => t.EquipmentId == eq.Id)
                    ? FieldCatalog.ValTuningActive
                    : FieldCatalog.ValTuningNone;

                var inProdFirst = inProdOrders.FirstOrDefault();

                var row = new ChartRowViewModel();
                row.Values[FieldCatalog.EquipmentName]        = eq.Name;
                row.Values[FieldCatalog.ProductionStatus]     = status;
                row.Values[FieldCatalog.LoadLevel]            = load;
                row.Values[FieldCatalog.TuningStatus]         = tuning;
                row.Values[FieldCatalog.ActiveOrderCount]     = pendingCount + inProdOrders.Count;
                row.Values[FieldCatalog.TotalPendingQty]      = totalPendingQty;
                row.Values[FieldCatalog.FeasibleProgramCount] = group.Count(ep => ep.ProductionStatus == TuningType.Feasible);
                row.Values[FieldCatalog.TotalProgramCount]    = group.Count();
                row.Values[FieldCatalog.CurrentProduct]       = inProdFirst != null
                    ? $"{inProdFirst.ProductName} · {inProdFirst.ProcessName}"
                    : null;
                row.Values[FieldCatalog.ProgressCompleted]    = equipOrders
                    .Where(o => o.Status == OrderProductionStatus.Completed).Sum(o => o.Quantity ?? 0);
                row.Values[FieldCatalog.ProgressTarget]       = equipOrders
                    .Where(o => o.Status != OrderProductionStatus.Cancelled).Sum(o => o.Quantity ?? 0);

                FinishRow(row, def);
                rows.Add(row);
            }

            return rows;
        }

        private List<ChartRowViewModel> BuildScheduleRows(ChartDefinition def)
        {
            var today = DateTime.Today;
            var rows = new List<ChartRowViewModel>();

            foreach (var ui in _schedules.Select(ScheduleUiModel.FromEntity))
            {
                var activeOrders = _orders
                    .Where(o => o.ScheduleId == ui.ScheduleId && o.Status != OrderProductionStatus.Cancelled)
                    .ToList();

                var row = new ChartRowViewModel();
                row.Values[FieldCatalog.ScheduleId]           = ui.ScheduleId;
                row.Values[FieldCatalog.Brand]                = ui.BrandName;
                row.Values[FieldCatalog.PartNo]               = ui.PartNo;
                row.Values[FieldCatalog.Model]                = ui.ModelName;
                row.Values[FieldCatalog.Process]              = ui.ProcessName;
                row.Values[FieldCatalog.Quantity]             = ui.Quantity;
                row.Values[FieldCatalog.ActualQuantity]       = ui.ActualQuantity;
                row.Values[FieldCatalog.ScheduleStatus]       = ui.Status.ToString();
                row.Values[FieldCatalog.SopType]              = ui.SopType?.ToString();
                row.Values[FieldCatalog.ReceivedAt]           = ui.ReceivedAt;
                row.Values[FieldCatalog.WaitingDays]          = ui.ReceivedAt.HasValue
                    ? (today - ui.ReceivedAt.Value.Date).Days
                    : 0;
                row.Values[FieldCatalog.ActiveEquipmentCount] = activeOrders
                    .Select(o => o.EquipmentId).Distinct().Count();

                FinishRow(row, def);
                rows.Add(row);
            }

            return rows;
        }

        private static void FinishRow(ChartRowViewModel row, ChartDefinition def)
        {
            foreach (var field in FieldCatalog.For(def.DataSet))
                row.Display[field.FieldId] = FormatValue(field, row.Values.GetValueOrDefault(field.FieldId));

            if (def.Container.Type == ChartContainerType.Card)
                ApplyCardSlots(row, def);
            else if (def.Container.Type == ChartContainerType.Table)
                row.IndicatorBrushKey = ResolveValueBrushKey(
                    def.DataSet, def.Container.Table.IndicatorFieldId, row) ?? "IdleBrush";
        }

        private static void ApplyCardSlots(ChartRowViewModel row, ChartDefinition def)
        {
            var card = def.Container.Card;
            row.Title = row.Display.GetValueOrDefault(card.TitleFieldId) ?? "";
            row.BorderBrushKey = ResolveValueBrushKey(def.DataSet, card.BorderColorFieldId, row) ?? "BorderBrush";

            foreach (var chip in card.Chips.Take(ChartConstants.MaxChips))
            {
                var field = FieldCatalog.Find(def.DataSet, chip.FieldId);
                if (field == null) continue;

                var raw = row.Values.GetValueOrDefault(chip.FieldId)?.ToString();
                var value = raw == null ? null : field.Values.FirstOrDefault(v => v.Value == raw);

                // Enum 值 rank==0 視為「無值」（None 類），配合「有值才顯示」不出 chip
                var hasValue = field.Type == ChartFieldType.Enum
                    ? value is { Rank: > 0 }
                    : !string.IsNullOrEmpty(raw);
                if (!hasValue && chip.ShowOnlyWhenHasValue) continue;

                row.Chips.Add(new ChartChipItem(
                    row.Display.GetValueOrDefault(chip.FieldId) ?? "",
                    value?.BrushKey ?? "IdleBrush"));
            }

            foreach (var fieldId in card.SecondaryFieldIds.Take(ChartConstants.MaxSecondaryInfos))
                row.SecondaryInfos.Add(new ChartSecondaryItem(
                    ResolveFieldLabel(def.DataSet, fieldId),
                    row.Display.GetValueOrDefault(fieldId) ?? "—"));

            if (card.ProgressNumeratorFieldId != null && card.ProgressDenominatorFieldId != null)
            {
                var num = ToDouble(row.Values.GetValueOrDefault(card.ProgressNumeratorFieldId));
                var den = ToDouble(row.Values.GetValueOrDefault(card.ProgressDenominatorFieldId));
                row.HasProgress = true;
                row.ProgressPercent = den > 0 ? Math.Clamp(num / den * 100, 0, 100) : 0;
                row.ProgressText = den > 0
                    ? $"{num.ToString("N0", CultureInfo.CurrentCulture)} / {den.ToString("N0", CultureInfo.CurrentCulture)}"
                    : "— / —";
            }
        }

        #endregion

        #region 統計列 / 容器 / 排序

        private void ComputeStats(ChartDefinition def)
        {
            if (!def.StatRow.Enabled) return;

            foreach (var item in def.StatRow.Items)
            {
                var value = item.Aggregate switch
                {
                    ChartAggregateType.Sum => _rows.Sum(r => ToDouble(r.Values.GetValueOrDefault(item.FieldId))),
                    _ when string.IsNullOrEmpty(item.FieldId) => _rows.Count,
                    _ when item.FilterValue == null =>
                        _rows.Count(r => r.Values.GetValueOrDefault(item.FieldId) != null),
                    _ => _rows.Count(r => string.Equals(
                             r.Values.GetValueOrDefault(item.FieldId)?.ToString(),
                             item.FilterValue, StringComparison.Ordinal)),
                };

                StatItems.Add(new ChartStatItemViewModel(
                    ResolveLabel(item.LabelKey, item.Label),
                    value.ToString("N0", CultureInfo.CurrentCulture),
                    item.ColorKey ?? "TextPrimaryBrush"));
            }
        }

        private void ApplyContainer(ChartDefinition def)
        {
            IsStatRowVisible = def.StatRow.Enabled && StatItems.Count > 0;
            IsCardContainer  = def.Container.Type == ChartContainerType.Card;
            IsTableContainer = def.Container.Type == ChartContainerType.Table;

            // IndicatorFieldId 先於 TableColumns 設定：code-behind 於 TableColumns 變更時重建欄位
            IndicatorFieldId = def.Container.Table.IndicatorFieldId;
            TableColumns = IsTableContainer
                ? def.Container.Table.ColumnFieldIds
                    .Select(id => new ChartTableColumn(id, ResolveFieldLabel(def.DataSet, id)))
                    .ToList()
                : Array.Empty<ChartTableColumn>();
        }

        #endregion

        #region 篩選列（篩選欄位 / 快捷按鈕 / 排序 / 恢復預設）

        private void BuildFilterRow(ChartDefinition def)
        {
            _suppressRefresh = true;
            try
            {
                ClearFilterRow();

                foreach (var fieldId in def.FilterRow.FilterFieldIds.Take(ChartConstants.MaxFilterFields))
                {
                    var field = FieldCatalog.Find(def.DataSet, fieldId);
                    if (field == null) continue;

                    var options = field.Type == ChartFieldType.Enum
                        ? new[] { new ChartFilterOption(null, Properties.Resources.ChartFilterAll) }
                            .Concat(field.Values.Select(v => new ChartFilterOption(
                                v.Value, Properties.Resources.ResourceManager.GetString(v.LabelKey) ?? v.Value)))
                            .ToList()
                        : new List<ChartFilterOption>();

                    var filter = new ChartFilterFieldViewModel(
                        fieldId, ResolveFieldLabel(def.DataSet, fieldId), field.Type, options);
                    filter.PropertyChanged += OnFilterConditionChanged;
                    FilterFields.Add(filter);
                }

                foreach (var cfg in def.FilterRow.QuickButtons.Take(ChartConstants.MaxQuickFilterButtons))
                {
                    var field = FieldCatalog.Find(def.DataSet, cfg.FieldId);
                    if (field?.CanQuickFilter != true) continue;

                    // 標籤：LabelKey > 自訂 Label > 值標籤
                    var label = cfg.LabelKey != null
                        ? Properties.Resources.ResourceManager.GetString(cfg.LabelKey) ?? cfg.Value
                        : !string.IsNullOrEmpty(cfg.Label) ? cfg.Label : ResolveEnumLabel(field, cfg.Value);

                    var button = new ChartQuickButtonViewModel(cfg.FieldId, cfg.Value, label);
                    button.PropertyChanged += OnQuickButtonChanged;
                    QuickButtons.Add(button);
                }

                foreach (var fieldId in def.FilterRow.SortOptionFieldIds.Take(ChartConstants.MaxSortOptions))
                    if (FieldCatalog.Find(def.DataSet, fieldId) != null)
                        SortOptions.Add(new ChartSortOptionItem(fieldId, ResolveFieldLabel(def.DataSet, fieldId)));

                SelectedSortOption = SortOptions.FirstOrDefault(o => o.FieldId == def.FilterRow.DefaultSortFieldId)
                                     ?? SortOptions.FirstOrDefault();
                IsSortDescending = def.FilterRow.DefaultSortDirection == ChartSortDirection.Descending;

                IsFilterRowVisible = def.FilterRow.Enabled
                    && (FilterFields.Count > 0 || QuickButtons.Count > 0 || SortOptions.Count > 0);
            }
            finally
            {
                _suppressRefresh = false;
            }

            ApplySort();
            RowsView.Refresh();
        }

        private void ClearFilterRow()
        {
            foreach (var f in FilterFields) f.PropertyChanged -= OnFilterConditionChanged;
            foreach (var b in QuickButtons) b.PropertyChanged -= OnQuickButtonChanged;
            FilterFields.Clear();
            QuickButtons.Clear();
            SortOptions.Clear();
        }

        /// <summary>ICollectionView 過濾：篩選欄位與快捷按鈕全部 AND 疊加</summary>
        private bool FilterRow(object obj)
        {
            if (obj is not ChartRowViewModel row) return false;
            return FilterFields.All(f => f.Matches(row))
                && QuickButtons.All(b => b.Matches(row));
        }

        private void OnFilterConditionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressRefresh) return;
            RowsView.Refresh();
        }

        private void OnQuickButtonChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressRefresh) return;
            if (e.PropertyName != nameof(ChartQuickButtonViewModel.IsActive)) return;

            // 同欄位互斥：後按覆蓋前按
            if (sender is ChartQuickButtonViewModel { IsActive: true } pressed)
            {
                _suppressRefresh = true;
                try
                {
                    foreach (var other in QuickButtons.Where(b => b != pressed && b.FieldId == pressed.FieldId))
                        other.IsActive = false;
                }
                finally { _suppressRefresh = false; }
            }

            RowsView.Refresh();
        }

        private void ApplySort()
        {
            var def = SelectedTab?.Definition;
            if (def == null || RowsView is not ListCollectionView lcv) return;

            var fieldId = SelectedSortOption?.FieldId ?? def.FilterRow.DefaultSortFieldId;
            var direction = IsSortDescending ? ChartSortDirection.Descending : ChartSortDirection.Ascending;
            lcv.CustomSort = ChartRowComparer.Create(def.DataSet, fieldId, direction);
        }

        partial void OnSelectedSortOptionChanged(ChartSortOptionItem? value)
        {
            if (_suppressRefresh) return;
            ApplySort();
        }

        partial void OnIsSortDescendingChanged(bool value)
        {
            if (_suppressRefresh) return;
            ApplySort();
        }

        [RelayCommand]
        private void ToggleSortDirection() => IsSortDescending = !IsSortDescending;

        /// <summary>恢復預設：篩選值回預設＋快捷全釋放＋排序回預設欄位與方向</summary>
        [RelayCommand]
        private void RestoreDefaults()
        {
            var def = SelectedTab?.Definition;
            if (def == null) return;

            _suppressRefresh = true;
            try
            {
                foreach (var f in FilterFields) f.Reset();
                foreach (var b in QuickButtons) b.IsActive = false;
                SelectedSortOption = SortOptions.FirstOrDefault(o => o.FieldId == def.FilterRow.DefaultSortFieldId)
                                     ?? SortOptions.FirstOrDefault();
                IsSortDescending = def.FilterRow.DefaultSortDirection == ChartSortDirection.Descending;
            }
            finally { _suppressRefresh = false; }

            ApplySort();
            RowsView.Refresh();
        }

        #endregion

        #region 檢視端縮放（檔位存本機 Properties.Settings）

        public bool CanZoomIn => Array.IndexOf(ChartConstants.ZoomLevels, ZoomPercent) < ChartConstants.ZoomLevels.Length - 1;
        public bool CanZoomOut => Array.IndexOf(ChartConstants.ZoomLevels, ZoomPercent) > 0;

        [RelayCommand(CanExecute = nameof(CanZoomIn))]
        private void ZoomIn() => StepZoom(+1);

        [RelayCommand(CanExecute = nameof(CanZoomOut))]
        private void ZoomOut() => StepZoom(-1);

        private void StepZoom(int step)
        {
            var index = Array.IndexOf(ChartConstants.ZoomLevels, ZoomPercent);
            var next = Math.Clamp(index + step, 0, ChartConstants.ZoomLevels.Length - 1);
            ZoomPercent = ChartConstants.ZoomLevels[next];
        }

        partial void OnZoomPercentChanged(int value)
        {
            OnPropertyChanged(nameof(ZoomScale));
            ZoomInCommand.NotifyCanExecuteChanged();
            ZoomOutCommand.NotifyCanExecuteChanged();

            Properties.Settings.Default.ChartZoomPercent = value;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region 格式化與標籤

        private static string FormatValue(ChartFieldDescriptor field, object? raw)
        {
            if (raw == null) return "—";
            return field.Type switch
            {
                ChartFieldType.Enum   => ResolveEnumLabel(field, raw.ToString() ?? ""),
                ChartFieldType.Number => Convert.ToDouble(raw, CultureInfo.InvariantCulture)
                                             .ToString("N0", CultureInfo.CurrentCulture),
                ChartFieldType.Date   => ((DateTime)raw).ToString("yyyy-MM-dd"),
                _                     => raw.ToString() ?? "—",
            };
        }

        private static string ResolveEnumLabel(ChartFieldDescriptor field, string value)
        {
            var v = field.Values.FirstOrDefault(x => x.Value == value);
            return v == null ? value
                : Properties.Resources.ResourceManager.GetString(v.LabelKey) ?? value;
        }

        /// <summary>取列在指定 Enum 欄位當前值的狀態色 key（FieldCatalog 值宣告）；查無回 null</summary>
        private static string? ResolveValueBrushKey(ChartDataSet dataSet, string? fieldId, ChartRowViewModel row)
        {
            if (string.IsNullOrEmpty(fieldId)) return null;
            var field = FieldCatalog.Find(dataSet, fieldId);
            var raw = row.Values.GetValueOrDefault(fieldId)?.ToString();
            return field?.Values.FirstOrDefault(v => v.Value == raw)?.BrushKey;
        }

        private static string ResolveLabel(string? labelKey, string fallback)
            => labelKey != null
                ? Properties.Resources.ResourceManager.GetString(labelKey) ?? fallback
                : fallback;

        private static string ResolveFieldLabel(ChartDataSet dataSet, string fieldId)
        {
            var field = FieldCatalog.Find(dataSet, fieldId);
            return field == null ? fieldId
                : Properties.Resources.ResourceManager.GetString(field.LabelKey) ?? fieldId;
        }

        private static double ToDouble(object? raw)
            => raw == null ? 0 : Convert.ToDouble(raw, CultureInfo.InvariantCulture);

        #endregion
    }
}
