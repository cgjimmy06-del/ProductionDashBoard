using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FProductionDashBoard.ViewModels
{
    /// <summary>設計器下拉選項：欄位（FieldId 空字串於統計項代表「全部列計數」）</summary>
    public record ChartFieldOption(string FieldId, string Label);

    /// <summary>設計器下拉選項：Enum 欄位值（Value=null 代表「無」）</summary>
    public record ChartEnumValueOption(string? Value, string Label);

    /// <summary>設計器下拉選項：資料來源</summary>
    public record ChartDataSetOption(ChartDataSet Value, string Label);

    /// <summary>設計器下拉選項：統計聚合方式</summary>
    public record ChartAggregateOption(ChartAggregateType Value, string Label);

    /// <summary>
    /// 統計項編輯器：聚合／欄位／值過濾（Enum 欄位限定）／標籤，直接寫回持有的 StatItemConfig。
    /// Sum 僅開放 Number 欄位（Enum 值無法加總）；標籤一經編輯即轉純文字（清除 LabelKey）。
    /// </summary>
    public partial class ChartStatItemEditorViewModel : ObservableObject
    {
        private readonly StatItemConfig _config;
        private readonly ChartDataSet _dataSet;
        private readonly Action _onChanged;
        private bool _initialized;

        public ChartStatItemEditorViewModel(StatItemConfig config, ChartDataSet dataSet, Action onChanged)
        {
            _config = config;
            _dataSet = dataSet;
            _onChanged = onChanged;

            AggregateOptions = new List<ChartAggregateOption>
            {
                new(ChartAggregateType.Count, Properties.Resources.ChartAggregateCount),
                new(ChartAggregateType.Sum,   Properties.Resources.ChartAggregateSum),
            };
            selectedAggregate = AggregateOptions.FirstOrDefault(o => o.Value == config.Aggregate)
                                ?? AggregateOptions[0];
            fieldOptions = BuildFieldOptions(selectedAggregate.Value);
            selectedField = fieldOptions.FirstOrDefault(o => o.FieldId == config.FieldId)
                            ?? fieldOptions[0];
            valueOptions = BuildValueOptions(selectedField.FieldId);
            selectedFilterValue = valueOptions.FirstOrDefault(o => o.Value == config.FilterValue)
                                  ?? valueOptions.FirstOrDefault();
            label = ChartRowBuilder.ResolveLabel(config.LabelKey, config.Label);
            _initialized = true;
        }

        public StatItemConfig Config => _config;
        public IReadOnlyList<ChartAggregateOption> AggregateOptions { get; }

        [ObservableProperty] private ChartAggregateOption selectedAggregate;
        [ObservableProperty] private IReadOnlyList<ChartFieldOption> fieldOptions;
        [ObservableProperty] private ChartFieldOption? selectedField;
        [ObservableProperty] private IReadOnlyList<ChartEnumValueOption> valueOptions;
        [ObservableProperty] private ChartEnumValueOption? selectedFilterValue;
        [ObservableProperty] private string label;

        /// <summary>值過濾僅 Enum 欄位適用</summary>
        public bool IsEnumField
            => SelectedField != null
               && FieldCatalog.Find(_dataSet, SelectedField.FieldId)?.Type == ChartFieldType.Enum;

        partial void OnSelectedAggregateChanged(ChartAggregateOption value)
        {
            if (!_initialized) return;
            _config.Aggregate = value.Value;
            FieldOptions = BuildFieldOptions(value.Value);
            if (SelectedField == null || FieldOptions.All(o => o.FieldId != SelectedField.FieldId))
                SelectedField = FieldOptions[0];   // setter 觸發寫回與預覽重建
            else
                _onChanged();
        }

        partial void OnSelectedFieldChanged(ChartFieldOption? value)
        {
            if (!_initialized) return;
            _config.FieldId = value?.FieldId ?? "";
            ValueOptions = BuildValueOptions(_config.FieldId);
            OnPropertyChanged(nameof(IsEnumField));

            var restored = ValueOptions.FirstOrDefault(o => o.Value == _config.FilterValue)
                           ?? ValueOptions.FirstOrDefault();
            if (!Equals(SelectedFilterValue, restored))
                SelectedFilterValue = restored;    // setter 觸發寫回與預覽重建
            else
                _onChanged();
        }

        partial void OnSelectedFilterValueChanged(ChartEnumValueOption? value)
        {
            if (!_initialized) return;
            _config.FilterValue = value?.Value;
            _onChanged();
        }

        partial void OnLabelChanged(string value)
        {
            if (!_initialized) return;
            _config.Label = value;
            _config.LabelKey = null;   // 編輯後轉純文字
            _onChanged();
        }

        private IReadOnlyList<ChartFieldOption> BuildFieldOptions(ChartAggregateType aggregate)
        {
            var fields = FieldCatalog.For(_dataSet).Where(f => f.CanStat);
            if (aggregate == ChartAggregateType.Sum)
                return fields.Where(f => f.Type == ChartFieldType.Number)
                    .Select(f => new ChartFieldOption(f.FieldId, ChartRowBuilder.ResolveFieldLabel(_dataSet, f.FieldId)))
                    .ToList();

            // Count：首項「全部（列數）」＝ FieldId 空字串
            return new[] { new ChartFieldOption("", Properties.Resources.ChartStatFieldAllRows) }
                .Concat(fields.Select(f => new ChartFieldOption(f.FieldId, ChartRowBuilder.ResolveFieldLabel(_dataSet, f.FieldId))))
                .ToList();
        }

        private IReadOnlyList<ChartEnumValueOption> BuildValueOptions(string fieldId)
        {
            var field = string.IsNullOrEmpty(fieldId) ? null : FieldCatalog.Find(_dataSet, fieldId);
            if (field == null || field.Type != ChartFieldType.Enum)
                return Array.Empty<ChartEnumValueOption>();

            return new[] { new ChartEnumValueOption(null, Properties.Resources.ChartFilterValueNone) }
                .Concat(field.Values.Select(v => new ChartEnumValueOption(
                    v.Value, Properties.Resources.ResourceManager.GetString(v.LabelKey) ?? v.Value)))
                .ToList();
        }
    }

    /// <summary>
    /// 單一欄位下拉的清單項（篩選欄位／排序選項共用）：僅持有選擇，
    /// 清單與 ChartDefinition 的同步由 ChartDesignerViewModel 的 Sync 方法統一處理。
    /// </summary>
    public partial class ChartFieldPickEditorViewModel : ObservableObject
    {
        private readonly Action _onChanged;
        private bool _initialized;

        public ChartFieldPickEditorViewModel(string fieldId, IReadOnlyList<ChartFieldOption> options, Action onChanged)
        {
            _onChanged = onChanged;
            Options = options;
            selectedField = options.FirstOrDefault(o => o.FieldId == fieldId) ?? options.FirstOrDefault();
            _initialized = true;
        }

        public IReadOnlyList<ChartFieldOption> Options { get; }

        [ObservableProperty] private ChartFieldOption? selectedField;

        partial void OnSelectedFieldChanged(ChartFieldOption? value)
        {
            if (!_initialized) return;
            _onChanged();
        }
    }

    /// <summary>
    /// 快捷按鈕編輯器：選欄位（限可快捷的 Enum 欄位）→選值→選填自訂標籤，直接寫回 QuickFilterButtonConfig。
    /// 自訂標籤一經編輯即清除 LabelKey；留空＝顯示值標籤。
    /// </summary>
    public partial class ChartQuickButtonEditorViewModel : ObservableObject
    {
        private readonly QuickFilterButtonConfig _config;
        private readonly ChartDataSet _dataSet;
        private readonly Action _onChanged;
        private bool _initialized;

        public ChartQuickButtonEditorViewModel(QuickFilterButtonConfig config, ChartDataSet dataSet, Action onChanged)
        {
            _config = config;
            _dataSet = dataSet;
            _onChanged = onChanged;

            FieldOptions = FieldCatalog.For(dataSet).Where(f => f.CanQuickFilter)
                .Select(f => new ChartFieldOption(f.FieldId, ChartRowBuilder.ResolveFieldLabel(dataSet, f.FieldId)))
                .ToList();
            selectedField = FieldOptions.FirstOrDefault(o => o.FieldId == config.FieldId)
                            ?? FieldOptions.FirstOrDefault();
            valueOptions = BuildValueOptions(selectedField?.FieldId);
            selectedValue = valueOptions.FirstOrDefault(o => o.Value == config.Value)
                            ?? valueOptions.FirstOrDefault();
            label = config.LabelKey != null
                ? Properties.Resources.ResourceManager.GetString(config.LabelKey) ?? config.Label
                : config.Label;
            _initialized = true;
        }

        public QuickFilterButtonConfig Config => _config;
        public IReadOnlyList<ChartFieldOption> FieldOptions { get; }

        [ObservableProperty] private ChartFieldOption? selectedField;
        [ObservableProperty] private IReadOnlyList<ChartEnumValueOption> valueOptions;
        [ObservableProperty] private ChartEnumValueOption? selectedValue;
        [ObservableProperty] private string label;

        partial void OnSelectedFieldChanged(ChartFieldOption? value)
        {
            if (!_initialized) return;
            _config.FieldId = value?.FieldId ?? "";
            ValueOptions = BuildValueOptions(value?.FieldId);

            var restored = ValueOptions.FirstOrDefault(o => o.Value == _config.Value)
                           ?? ValueOptions.FirstOrDefault();
            if (!Equals(SelectedValue, restored))
                SelectedValue = restored;   // setter 觸發寫回與預覽重建
            else
                _onChanged();
        }

        partial void OnSelectedValueChanged(ChartEnumValueOption? value)
        {
            if (!_initialized) return;
            _config.Value = value?.Value ?? "";
            _onChanged();
        }

        partial void OnLabelChanged(string value)
        {
            if (!_initialized) return;
            _config.Label = value;
            _config.LabelKey = null;   // 編輯後轉純文字
            _onChanged();
        }

        private IReadOnlyList<ChartEnumValueOption> BuildValueOptions(string? fieldId)
        {
            var field = string.IsNullOrEmpty(fieldId) ? null : FieldCatalog.Find(_dataSet, fieldId!);
            if (field == null) return Array.Empty<ChartEnumValueOption>();

            // 快捷按鈕必有值，不提供「無」選項
            return field.Values
                .Select(v => new ChartEnumValueOption(
                    v.Value, Properties.Resources.ResourceManager.GetString(v.LabelKey) ?? v.Value))
                .ToList();
        }
    }
}
