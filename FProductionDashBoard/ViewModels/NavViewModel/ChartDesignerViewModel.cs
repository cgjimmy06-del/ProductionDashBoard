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
        private readonly Action<bool> _onClose;

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
            IChartDefinitionStore store, DashboardCoreServices core, Action<bool> onClose)
        {
            _store = store;
            _core = core;
            _onClose = onClose;
            IsNew = isNew;

            WorkingDefinition = Clone(source);
            SanitizedCount = ChartDefinitionSanitizer.Sanitize(WorkingDefinition);
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
