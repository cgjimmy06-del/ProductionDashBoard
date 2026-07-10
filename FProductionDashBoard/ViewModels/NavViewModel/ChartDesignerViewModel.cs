using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace FProductionDashBoard.ViewModels
{
    /// <summary>
    /// 圖表設計器：對定義的 deep-clone（WorkingDefinition）編輯，「儲存」才寫 store、
    /// 「取消/返回」直接丟棄；左預覽以 ChartSampleData 假資料經 ChartRowBuilder 渲染，不打 DB。
    /// 每次開啟由 ChartViewModel new 一份，關閉後設 null 交 GC。
    /// </summary>
    public partial class ChartDesignerViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
        private readonly IChartDefinitionStore _store;
        private readonly IDialogService _dialog;
        private readonly Action<bool> _onClose;

        /// <summary>InitEditorState／資料來源切換重建期間抑制編輯屬性寫回</summary>
        private bool _suppressEditorSync;

        /// <summary>編輯暫存（來源定義的 deep-clone，與 store 快取實例隔離）</summary>
        public ChartDefinition WorkingDefinition { get; }

        public bool IsNew { get; }

        /// <summary>開啟時淨化剔除的失效引用數（>0 時顯示提示）</summary>
        public int SanitizedCount { get; }
        public bool HasSanitizeNotice => SanitizedCount > 0;
        public string SanitizeNotice
            => string.Format(Properties.Resources.ChartDesignerSanitizedNotice, SanitizedCount);

        /// <summary>頂列顯示名稱（NameKey 優先，比照 ChartTabItemViewModel）</summary>
        public string DisplayName => WorkingDefinition.NameKey != null
            ? Properties.Resources.ResourceManager.GetString(WorkingDefinition.NameKey) ?? WorkingDefinition.Name
            : WorkingDefinition.Name;

        // ----- 右編輯區（C2：基本／統計列／篩選列；寫回 WorkingDefinition 並即時重建預覽） -----

        public IReadOnlyList<ChartDataSetOption> DataSetOptions { get; }

        [ObservableProperty] private string chartName = "";
        [ObservableProperty] private int sortOrder;
        [ObservableProperty] private ChartDataSetOption? selectedDataSet;
        [ObservableProperty] private bool isStatRowEnabled;
        [ObservableProperty] private bool isFilterRowEnabled;
        [ObservableProperty] private IReadOnlyList<ChartFieldOption> defaultSortOptions = Array.Empty<ChartFieldOption>();
        [ObservableProperty] private ChartFieldOption? selectedDefaultSort;
        [ObservableProperty] private bool isDefaultSortDescending;

        public ObservableCollection<ChartStatItemEditorViewModel> StatItemEditors { get; } = new();
        public ObservableCollection<ChartFieldPickEditorViewModel> FilterFieldEditors { get; } = new();
        public ObservableCollection<ChartQuickButtonEditorViewModel> QuickButtonEditors { get; } = new();
        public ObservableCollection<ChartFieldPickEditorViewModel> SortOptionEditors { get; } = new();

        public bool CanAddStatItem    => StatItemEditors.Count < ChartConstants.MaxStatItems;
        public bool CanAddFilterField => FilterFieldEditors.Count < ChartConstants.MaxFilterFields;
        public bool CanAddQuickButton => QuickButtonEditors.Count < ChartConstants.MaxQuickFilterButtons;
        public bool CanAddSortOption  => SortOptionEditors.Count < ChartConstants.MaxSortOptions;

        partial void OnChartNameChanged(string value)
        {
            if (_suppressEditorSync) return;
            WorkingDefinition.Name = value;
            WorkingDefinition.NameKey = null;   // 更名即轉純文字
            OnPropertyChanged(nameof(DisplayName));
        }

        partial void OnSortOrderChanged(int value)
        {
            if (_suppressEditorSync) return;
            WorkingDefinition.SortOrder = value;
        }

        partial void OnSelectedDataSetChanged(ChartDataSetOption? oldValue, ChartDataSetOption? newValue)
        {
            if (_suppressEditorSync || oldValue == null || newValue == null || oldValue.Value == newValue.Value)
                return;

            if (!_dialog.ShowConfirm(Properties.Resources.ChartDataSetSwitchConfirm))
            {
                RevertDataSetSelection(oldValue);
                return;
            }

            ApplyDataSetSwitch(newValue.Value);
        }

        partial void OnIsStatRowEnabledChanged(bool value)
        {
            if (_suppressEditorSync) return;
            WorkingDefinition.StatRow.Enabled = value;
            RebuildPreview();
        }

        partial void OnIsFilterRowEnabledChanged(bool value)
        {
            if (_suppressEditorSync) return;
            WorkingDefinition.FilterRow.Enabled = value;
            RebuildPreview();
        }

        partial void OnSelectedDefaultSortChanged(ChartFieldOption? value)
        {
            if (_suppressEditorSync) return;
            WorkingDefinition.FilterRow.DefaultSortFieldId = value?.FieldId ?? "";
            RebuildPreview();
        }

        partial void OnIsDefaultSortDescendingChanged(bool value)
        {
            if (_suppressEditorSync) return;
            WorkingDefinition.FilterRow.DefaultSortDirection =
                value ? ChartSortDirection.Descending : ChartSortDirection.Ascending;
            RebuildPreview();
        }

        /// <summary>取消切換時還原下拉選擇；UI 執行緒排入佇列以避開 ComboBox 選擇變更中的重入</summary>
        private void RevertDataSetSelection(ChartDataSetOption oldValue)
        {
            void Revert()
            {
                _suppressEditorSync = true;
                SelectedDataSet = oldValue;
                _suppressEditorSync = false;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && dispatcher.CheckAccess())
                dispatcher.BeginInvoke((Action)Revert);   // UI 執行緒：排入佇列避開選擇變更重入
            else
                Revert();   // 非 UI 執行緒（單元測試）直接還原
        }

        /// <summary>切換資料來源：重置所有欄位選擇（統計/篩選/容器），主名稱預填新資料集第一欄</summary>
        private void ApplyDataSetSwitch(ChartDataSet dataSet)
        {
            var def = WorkingDefinition;
            def.DataSet = dataSet;

            def.StatRow.Items.Clear();
            def.FilterRow.FilterFieldIds.Clear();
            def.FilterRow.QuickButtons.Clear();
            def.FilterRow.SortOptionFieldIds.Clear();
            def.FilterRow.DefaultSortFieldId = "";
            def.FilterRow.DefaultSortDirection = ChartSortDirection.Ascending;

            var card = def.Container.Card;
            card.BorderColorFieldId = "";
            card.TitleFieldId = FieldCatalog.For(dataSet)[0].FieldId;   // 預填第一欄，預覽開場即有內容
            card.Chips.Clear();
            card.SecondaryFieldIds.Clear();
            card.ProgressNumeratorFieldId = null;
            card.ProgressDenominatorFieldId = null;

            def.Container.Table.ColumnFieldIds.Clear();
            def.Container.Table.IndicatorFieldId = null;

            InitEditorState();
            RebuildPreview();
        }

        /// <summary>依 WorkingDefinition 重建右編輯區狀態（開啟時與資料來源切換後）</summary>
        private void InitEditorState()
        {
            _suppressEditorSync = true;
            try
            {
                ChartName = DisplayName;
                SortOrder = WorkingDefinition.SortOrder;
                SelectedDataSet = DataSetOptions.First(o => o.Value == WorkingDefinition.DataSet);
                IsStatRowEnabled = WorkingDefinition.StatRow.Enabled;
                IsFilterRowEnabled = WorkingDefinition.FilterRow.Enabled;

                StatItemEditors.Clear();
                foreach (var item in WorkingDefinition.StatRow.Items)
                    StatItemEditors.Add(new ChartStatItemEditorViewModel(item, WorkingDefinition.DataSet, RebuildPreview));

                FilterFieldEditors.Clear();
                foreach (var id in WorkingDefinition.FilterRow.FilterFieldIds)
                    FilterFieldEditors.Add(new ChartFieldPickEditorViewModel(id, FilterFieldOptions(), OnFilterFieldsEdited));

                QuickButtonEditors.Clear();
                foreach (var cfg in WorkingDefinition.FilterRow.QuickButtons)
                    QuickButtonEditors.Add(new ChartQuickButtonEditorViewModel(cfg, WorkingDefinition.DataSet, RebuildPreview));

                SortOptionEditors.Clear();
                foreach (var id in WorkingDefinition.FilterRow.SortOptionFieldIds)
                    SortOptionEditors.Add(new ChartFieldPickEditorViewModel(id, SortFieldOptions(), OnSortOptionsEdited));

                RefreshDefaultSortOptions();
                IsDefaultSortDescending =
                    WorkingDefinition.FilterRow.DefaultSortDirection == ChartSortDirection.Descending;
            }
            finally
            {
                _suppressEditorSync = false;
            }
            NotifyListLimits();
        }

        /// <summary>清單項變更後同步回定義（篩選欄位採位置對應整批重建）</summary>
        private void OnFilterFieldsEdited()
        {
            WorkingDefinition.FilterRow.FilterFieldIds.Clear();
            WorkingDefinition.FilterRow.FilterFieldIds.AddRange(
                FilterFieldEditors.Where(e => e.SelectedField != null).Select(e => e.SelectedField!.FieldId));
            RebuildPreview();
        }

        private void OnSortOptionsEdited()
        {
            WorkingDefinition.FilterRow.SortOptionFieldIds.Clear();
            WorkingDefinition.FilterRow.SortOptionFieldIds.AddRange(
                SortOptionEditors.Where(e => e.SelectedField != null).Select(e => e.SelectedField!.FieldId));
            RefreshDefaultSortOptions();
            RebuildPreview();
        }

        /// <summary>預設排序選項＝「（主名稱）」＋當前排序選項；被移除的欄位落回主名稱</summary>
        private void RefreshDefaultSortOptions()
        {
            var options = new List<ChartFieldOption>
            {
                new("", Properties.Resources.ChartDefaultSortByTitle),
            };
            options.AddRange(WorkingDefinition.FilterRow.SortOptionFieldIds
                .Where(id => FieldCatalog.Find(WorkingDefinition.DataSet, id) != null)
                .Select(id => new ChartFieldOption(id, ChartRowBuilder.ResolveFieldLabel(WorkingDefinition.DataSet, id))));
            DefaultSortOptions = options;

            var current = WorkingDefinition.FilterRow.DefaultSortFieldId;
            var restored = options.FirstOrDefault(o => o.FieldId == current) ?? options[0];

            var wasSuppressed = _suppressEditorSync;
            _suppressEditorSync = true;
            SelectedDefaultSort = restored;
            _suppressEditorSync = wasSuppressed;

            if (restored.FieldId != current)
                WorkingDefinition.FilterRow.DefaultSortFieldId = restored.FieldId;
        }

        private IReadOnlyList<ChartFieldOption> FilterFieldOptions()
            => FieldCatalog.For(WorkingDefinition.DataSet).Where(f => f.CanFilter)
                .Select(f => new ChartFieldOption(
                    f.FieldId, ChartRowBuilder.ResolveFieldLabel(WorkingDefinition.DataSet, f.FieldId)))
                .ToList();

        private IReadOnlyList<ChartFieldOption> SortFieldOptions()
            => FieldCatalog.For(WorkingDefinition.DataSet).Where(f => f.CanSort)
                .Select(f => new ChartFieldOption(
                    f.FieldId, ChartRowBuilder.ResolveFieldLabel(WorkingDefinition.DataSet, f.FieldId)))
                .ToList();

        // ----- 清單新增/移除（達上限「＋新增」反灰） -----

        [RelayCommand(CanExecute = nameof(CanAddStatItem))]
        private void AddStatItem()
        {
            var config = new StatItemConfig();   // 預設 Count＋全部列計數
            WorkingDefinition.StatRow.Items.Add(config);
            StatItemEditors.Add(new ChartStatItemEditorViewModel(config, WorkingDefinition.DataSet, RebuildPreview));
            NotifyListLimits();
            RebuildPreview();
        }

        [RelayCommand]
        private void RemoveStatItem(ChartStatItemEditorViewModel item)
        {
            WorkingDefinition.StatRow.Items.Remove(item.Config);
            StatItemEditors.Remove(item);
            NotifyListLimits();
            RebuildPreview();
        }

        [RelayCommand(CanExecute = nameof(CanAddFilterField))]
        private void AddFilterField()
        {
            var options = FilterFieldOptions();
            if (options.Count == 0) return;
            var used = FilterFieldEditors.Select(e => e.SelectedField?.FieldId).ToHashSet();
            var first = options.FirstOrDefault(o => !used.Contains(o.FieldId)) ?? options[0];
            FilterFieldEditors.Add(new ChartFieldPickEditorViewModel(first.FieldId, options, OnFilterFieldsEdited));
            NotifyListLimits();
            OnFilterFieldsEdited();
        }

        [RelayCommand]
        private void RemoveFilterField(ChartFieldPickEditorViewModel item)
        {
            FilterFieldEditors.Remove(item);
            NotifyListLimits();
            OnFilterFieldsEdited();
        }

        [RelayCommand(CanExecute = nameof(CanAddQuickButton))]
        private void AddQuickButton()
        {
            var field = FieldCatalog.For(WorkingDefinition.DataSet)
                .FirstOrDefault(f => f.CanQuickFilter && f.Values.Count > 0);
            if (field == null) return;

            var config = new QuickFilterButtonConfig { FieldId = field.FieldId, Value = field.Values[0].Value };
            WorkingDefinition.FilterRow.QuickButtons.Add(config);
            QuickButtonEditors.Add(new ChartQuickButtonEditorViewModel(config, WorkingDefinition.DataSet, RebuildPreview));
            NotifyListLimits();
            RebuildPreview();
        }

        [RelayCommand]
        private void RemoveQuickButton(ChartQuickButtonEditorViewModel item)
        {
            WorkingDefinition.FilterRow.QuickButtons.Remove(item.Config);
            QuickButtonEditors.Remove(item);
            NotifyListLimits();
            RebuildPreview();
        }

        [RelayCommand(CanExecute = nameof(CanAddSortOption))]
        private void AddSortOption()
        {
            var options = SortFieldOptions();
            if (options.Count == 0) return;
            var used = SortOptionEditors.Select(e => e.SelectedField?.FieldId).ToHashSet();
            var first = options.FirstOrDefault(o => !used.Contains(o.FieldId)) ?? options[0];
            SortOptionEditors.Add(new ChartFieldPickEditorViewModel(first.FieldId, options, OnSortOptionsEdited));
            NotifyListLimits();
            OnSortOptionsEdited();
        }

        [RelayCommand]
        private void RemoveSortOption(ChartFieldPickEditorViewModel item)
        {
            SortOptionEditors.Remove(item);
            NotifyListLimits();
            OnSortOptionsEdited();
        }

        private void NotifyListLimits()
        {
            OnPropertyChanged(nameof(CanAddStatItem));
            OnPropertyChanged(nameof(CanAddFilterField));
            OnPropertyChanged(nameof(CanAddQuickButton));
            OnPropertyChanged(nameof(CanAddSortOption));
            AddStatItemCommand.NotifyCanExecuteChanged();
            AddFilterFieldCommand.NotifyCanExecuteChanged();
            AddQuickButtonCommand.NotifyCanExecuteChanged();
            AddSortOptionCommand.NotifyCanExecuteChanged();
        }

        // ----- 左預覽狀態（RebuildPreview 重建，採 Clear 重填） -----

        public ObservableCollection<ChartStatItemViewModel> PreviewStatItems { get; } = new();
        public ObservableCollection<ChartRowViewModel> PreviewRows { get; } = new();
        public ObservableCollection<ChartRowViewModel> PreviewSampleRows { get; } = new();
        public ObservableCollection<string> PreviewFilterLabels { get; } = new();
        public ObservableCollection<string> PreviewQuickLabels { get; } = new();

        [ObservableProperty] private bool isPreviewStatVisible;
        [ObservableProperty] private bool isPreviewFilterVisible;
        [ObservableProperty] private bool isPreviewCard;
        [ObservableProperty] private bool isPreviewTable;
        [ObservableProperty] private string? previewSortLabel;
        [ObservableProperty] private string? previewIndicatorFieldId;
        [ObservableProperty] private IReadOnlyList<ChartTableColumn> previewTableColumns
            = Array.Empty<ChartTableColumn>();

        public ChartDesignerViewModel(ChartDefinition source, bool isNew,
            IChartDefinitionStore store, DashboardCoreServices core,
            IDialogService dialog, Action<bool> onClose)
        {
            _store = store;
            _core = core;
            _dialog = dialog;
            _onClose = onClose;
            IsNew = isNew;

            WorkingDefinition = Clone(source);
            SanitizedCount = ChartDefinitionSanitizer.Sanitize(WorkingDefinition);

            DataSetOptions = new List<ChartDataSetOption>
            {
                new(ChartDataSet.Equipment, Properties.Resources.ChartDataSetEquipment),
                new(ChartDataSet.Schedule,  Properties.Resources.ChartDataSetSchedule),
            };
            InitEditorState();
            RebuildPreview();
        }

        /// <summary>JSON round-trip deep-clone（store 快取為同一實例，編輯前必須隔離）</summary>
        internal static ChartDefinition Clone(ChartDefinition source)
            => JsonSerializer.Deserialize<ChartDefinition>(JsonSerializer.Serialize(source))!;

        [RelayCommand]
        private void Save()
        {
            try
            {
                _store.Save(WorkingDefinition);
                _onClose(true);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[圖表設計器] 儲存圖表失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[Save] {ex.Message}");
            }
        }

        [RelayCommand]
        private void Cancel() => _onClose(false);

        /// <summary>依 WorkingDefinition 重建左預覽（開啟時與右側編輯變更時呼叫）</summary>
        public void RebuildPreview()
        {
            var def = WorkingDefinition;

            PreviewStatItems.Clear();
            PreviewRows.Clear();
            PreviewSampleRows.Clear();
            PreviewFilterLabels.Clear();
            PreviewQuickLabels.Clear();

            var rows = ChartSampleData.CreateRows(def.DataSet);
            foreach (var row in rows)
            {
                ChartRowBuilder.FinishRow(row, def);
                PreviewRows.Add(row);
            }
            if (rows.Count > 0)
                PreviewSampleRows.Add(rows[0]);

            foreach (var item in ChartRowBuilder.BuildStatItems(def, rows))
                PreviewStatItems.Add(item);
            IsPreviewStatVisible = def.StatRow.Enabled && PreviewStatItems.Count > 0;

            foreach (var fieldId in def.FilterRow.FilterFieldIds.Take(ChartConstants.MaxFilterFields))
                if (FieldCatalog.Find(def.DataSet, fieldId) != null)
                    PreviewFilterLabels.Add(ChartRowBuilder.ResolveFieldLabel(def.DataSet, fieldId));

            foreach (var cfg in def.FilterRow.QuickButtons.Take(ChartConstants.MaxQuickFilterButtons))
            {
                var field = FieldCatalog.Find(def.DataSet, cfg.FieldId);
                if (field?.CanQuickFilter == true)
                    PreviewQuickLabels.Add(ChartRowBuilder.ResolveQuickButtonLabel(field, cfg));
            }

            PreviewSortLabel = string.IsNullOrEmpty(def.FilterRow.DefaultSortFieldId)
                ? null
                : ChartRowBuilder.ResolveFieldLabel(def.DataSet, def.FilterRow.DefaultSortFieldId);
            IsPreviewFilterVisible = def.FilterRow.Enabled
                && (PreviewFilterLabels.Count > 0 || PreviewQuickLabels.Count > 0 || PreviewSortLabel != null);

            IsPreviewCard  = def.Container.Type == ChartContainerType.Card;
            IsPreviewTable = def.Container.Type == ChartContainerType.Table;

            // IndicatorFieldId 先於 TableColumns 設定：code-behind 於 TableColumns 變更時重建欄位
            PreviewIndicatorFieldId = def.Container.Table.IndicatorFieldId;
            PreviewTableColumns = IsPreviewTable
                ? def.Container.Table.ColumnFieldIds
                    .Where(id => FieldCatalog.Find(def.DataSet, id) != null)
                    .Select(id => new ChartTableColumn(id, ChartRowBuilder.ResolveFieldLabel(def.DataSet, id)))
                    .ToList()
                : Array.Empty<ChartTableColumn>();
        }
    }
}
