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
    /// <summary>
    /// 圖表頁：依 ChartDefinition 宣告式定義渲染統計列與容器（卡片牆/表格）。
    /// 來源資料於 LoadAsync 一次載入快取，頁籤切換僅在記憶體重建列，不重打 DB。
    /// </summary>
    public partial class ChartViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
        private readonly IChartDefinitionStore _store;
        private readonly IDialogService _dialog;

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

        // 設計器層（頁內雙層切換，比照 ScheduleView 焦點模式）
        [ObservableProperty] private bool isDesignerOpen;
        [ObservableProperty] private ChartDesignerViewModel? designer;

        /// <summary>檢視層可見性（無 Inverse converter，比照 ScheduleView 以互補屬性各自綁 Visibility）</summary>
        public bool IsViewerVisible => !IsDesignerOpen;

        partial void OnIsDesignerOpenChanged(bool value) => OnPropertyChanged(nameof(IsViewerVisible));

        /// <summary>重建/恢復預設期間抑制逐項刷新，結束後一次套用</summary>
        private bool _suppressRefresh;

        public ChartViewModel(DashboardCoreServices core, IChartDefinitionStore store, IDialogService dialog)
        {
            _core = core;
            _store = store;
            _dialog = dialog;
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
            // 設計中切出導覽再切回不重載（避免 ReloadTabs 打掉編輯狀態）
            if (IsDesignerOpen) return;

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
                if (Tabs.Count == 0)
                {
                    // DB 不可用時仍渲染頁籤與圖表骨架（現場永遠有畫面），統計顯示 0、容器空白
                    ApplyData(new(), new(), new(), new());
                    _core.Log.AddLog("[圖表] 載入資料失敗，請確認資料庫連線；圖表暫以空資料顯示", LogLevel.Error);
                }
                else
                {
                    _core.Log.AddLog("[圖表] 更新資料失敗，請確認資料庫連線；維持前次資料顯示", LogLevel.Error);
                }
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

        partial void OnSelectedTabChanged(ChartTabItemViewModel? value)
        {
            RebuildForDefinition();
            EditChartCommand.NotifyCanExecuteChanged();
        }

        private void RebuildForDefinition()
        {
            var def = SelectedTab?.Definition;

            _rows.Clear();
            StatItems.Clear();

            if (def == null)
            {
                ResetPresentation();
                return;
            }

            try
            {
                var rows = def.DataSet == ChartDataSet.Equipment
                    ? BuildEquipmentRows(def)
                    : BuildScheduleRows(def);
                foreach (var row in rows)
                    _rows.Add(row);

                ComputeStats(def);
                ApplyContainer(def);
                BuildFilterRow(def);
            }
            catch (Exception ex)
            {
                // 失效引用防護：定義損毀（如 DataSet 未知值）時該圖表顯示空白，不崩潰、不改寫檔案
                _rows.Clear();
                StatItems.Clear();
                ResetPresentation();
                _core.Log.AddLog("[圖表] 圖表定義渲染失敗，已改為空白顯示", LogLevel.Error);
                _core.Log.AddErrorLog($"[RebuildForDefinition] {ex.Message}");
            }
        }

        private void ResetPresentation()
        {
            IsStatRowVisible = false;
            IsCardContainer  = false;
            IsTableContainer = false;
            IndicatorFieldId = null;
            TableColumns = Array.Empty<ChartTableColumn>();
            ClearFilterRow();
            IsFilterRowVisible = false;
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

                ChartRowBuilder.FinishRow(row, def);
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

                ChartRowBuilder.FinishRow(row, def);
                rows.Add(row);
            }

            return rows;
        }

        #endregion

        #region 統計列 / 容器 / 排序

        private void ComputeStats(ChartDefinition def)
        {
            foreach (var item in ChartRowBuilder.BuildStatItems(def, _rows))
                StatItems.Add(item);
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
                    .Where(id => FieldCatalog.Find(def.DataSet, id) != null)   // 失效欄位唯讀容忍：跳過不渲染
                    .Select(id => new ChartTableColumn(id, ChartRowBuilder.ResolveFieldLabel(def.DataSet, id)))
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
                        fieldId, ChartRowBuilder.ResolveFieldLabel(def.DataSet, fieldId), field.Type, options);
                    filter.PropertyChanged += OnFilterConditionChanged;
                    FilterFields.Add(filter);
                }

                foreach (var cfg in def.FilterRow.QuickButtons.Take(ChartConstants.MaxQuickFilterButtons))
                {
                    var field = FieldCatalog.Find(def.DataSet, cfg.FieldId);
                    if (field?.CanQuickFilter != true) continue;

                    var button = new ChartQuickButtonViewModel(cfg.FieldId, cfg.Value,
                        ChartRowBuilder.ResolveQuickButtonLabel(field, cfg));
                    button.PropertyChanged += OnQuickButtonChanged;
                    QuickButtons.Add(button);
                }

                foreach (var fieldId in def.FilterRow.SortOptionFieldIds.Take(ChartConstants.MaxSortOptions))
                    if (FieldCatalog.Find(def.DataSet, fieldId) != null)
                        SortOptions.Add(new ChartSortOptionItem(fieldId, ChartRowBuilder.ResolveFieldLabel(def.DataSet, fieldId)));

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

        #region 設計器（開啟/關閉；C1 先接編輯入口，其餘命令於 C4 補齊）

        private bool CanEditChart() => SelectedTab is { IsDefault: false };

        [RelayCommand(CanExecute = nameof(CanEditChart))]
        private void EditChart()
        {
            if (SelectedTab == null) return;
            OpenDesigner(SelectedTab.Definition, isNew: false);
        }

        private void OpenDesigner(ChartDefinition source, bool isNew)
        {
            Designer = new ChartDesignerViewModel(source, isNew, _store, _core, _dialog, OnDesignerClosed);
            IsDesignerOpen = true;
        }

        private void OnDesignerClosed(bool saved)
        {
            IsDesignerOpen = false;
            Designer = null;      // 每次開啟 new 一份，關閉交 GC
            if (saved)
                ReloadTabs();     // 依 previousId 還原選中頁籤
        }

        #endregion
    }
}
