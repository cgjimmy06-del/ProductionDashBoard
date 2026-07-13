using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FProductionDashBoard.ViewModels
{
    public class ChartTabItemViewModel
    {
        public ChartTabItemViewModel(ChartDefinition definition) => Definition = definition;

        public ChartDefinition Definition { get; }
        public bool IsDefault => Definition.IsDefault;
        public string DisplayName => Definition.NameKey != null
            ? Properties.Resources.ResourceManager.GetString(Definition.NameKey) ?? Definition.Name
            : Definition.Name;
    }

    /// <summary>
    /// 篩選列的單一篩選欄位：型別決定控件（Enum→下拉、Text→關鍵字、Date→起訖區間）。
    /// 值變更由 ChartViewModel 訂閱 PropertyChanged 觸發 RowsView.Refresh。
    /// </summary>
    public partial class ChartFilterFieldViewModel : ObservableObject
    {
        public ChartFilterFieldViewModel(string fieldId, string label, ChartFieldType type,
            IReadOnlyList<ChartFilterOption> options)
        {
            FieldId = fieldId;
            Label = label;
            Type = type;
            Options = options;
            selectedOption = options.FirstOrDefault();   // 首項＝全部（Value=null）
        }

        public string FieldId { get; }
        public string Label { get; }
        public ChartFieldType Type { get; }
        public IReadOnlyList<ChartFilterOption> Options { get; }

        public bool IsEnum => Type == ChartFieldType.Enum;
        public bool IsText => Type == ChartFieldType.Text;
        public bool IsDate => Type == ChartFieldType.Date;

        [ObservableProperty] private ChartFilterOption? selectedOption;
        [ObservableProperty] private string keyword = "";
        [ObservableProperty] private DateTime? dateStart;
        [ObservableProperty] private DateTime? dateEnd;

        /// <summary>是否有生效中的條件</summary>
        public bool HasCondition => Type switch
        {
            ChartFieldType.Enum => SelectedOption?.Value != null,
            ChartFieldType.Text => !string.IsNullOrWhiteSpace(Keyword),
            ChartFieldType.Date => DateStart.HasValue || DateEnd.HasValue,
            _ => false,
        };

        public void Reset()
        {
            SelectedOption = Options.FirstOrDefault();
            Keyword = "";
            DateStart = null;
            DateEnd = null;
        }

        /// <summary>列值是否通過此欄位條件</summary>
        public bool Matches(ChartRowViewModel row)
        {
            if (!HasCondition) return true;
            var raw = row.Values.GetValueOrDefault(FieldId);

            return Type switch
            {
                ChartFieldType.Enum => string.Equals(raw?.ToString(), SelectedOption!.Value, StringComparison.Ordinal),
                ChartFieldType.Text => raw?.ToString()?.Contains(Keyword.Trim(), StringComparison.OrdinalIgnoreCase) == true,
                ChartFieldType.Date => raw is DateTime dt
                    && (!DateStart.HasValue || dt.Date >= DateStart.Value.Date)
                    && (!DateEnd.HasValue || dt.Date <= DateEnd.Value.Date),
                _ => true,
            };
        }
    }

    /// <summary>Enum 篩選選項；Value=null 代表「全部」</summary>
    public record ChartFilterOption(string? Value, string Label);

    /// <summary>快捷按鈕＝欄位＋固定值；IsActive 變更由 ChartViewModel 訂閱處理互斥與刷新</summary>
    public partial class ChartQuickButtonViewModel : ObservableObject
    {
        public ChartQuickButtonViewModel(string fieldId, string value, string label)
        {
            FieldId = fieldId;
            Value = value;
            Label = label;
        }

        public string FieldId { get; }
        public string Value { get; }
        public string Label { get; }

        [ObservableProperty] private bool isActive;

        public bool Matches(ChartRowViewModel row)
            => !IsActive
            || string.Equals(row.Values.GetValueOrDefault(FieldId)?.ToString(), Value, StringComparison.Ordinal);
    }

    /// <summary>排序下拉選項</summary>
    public record ChartSortOptionItem(string FieldId, string Label);

    /// <summary>
    /// 圖表列排序（單鍵）：Enum 欄位依 FieldCatalog rank、Number/Date 依值、Text 依字串；
    /// null 一律排最後，主名稱（Title）作次要排序保持穩定。
    /// </summary>
    public class ChartRowComparer : IComparer
    {
        private readonly string _fieldId;
        private readonly bool _descending;
        private readonly ChartFieldType _fieldType;
        private readonly Dictionary<string, int> _rankMap;

        private ChartRowComparer(string fieldId, bool descending, ChartFieldType fieldType, Dictionary<string, int> rankMap)
        {
            _fieldId = fieldId;
            _descending = descending;
            _fieldType = fieldType;
            _rankMap = rankMap;
        }

        public static ChartRowComparer Create(ChartDataSet dataSet, string fieldId, ChartSortDirection direction)
        {
            var field = FieldCatalog.Find(dataSet, fieldId);
            return new ChartRowComparer(
                fieldId,
                direction == ChartSortDirection.Descending,
                field?.Type ?? ChartFieldType.Text,
                field?.Values.ToDictionary(v => v.Value, v => v.Rank) ?? new Dictionary<string, int>());
        }

        public int Compare(object? x, object? y)
        {
            if (x is not ChartRowViewModel a || y is not ChartRowViewModel b) return 0;

            var va = string.IsNullOrEmpty(_fieldId) ? null : a.Values.GetValueOrDefault(_fieldId);
            var vb = string.IsNullOrEmpty(_fieldId) ? null : b.Values.GetValueOrDefault(_fieldId);

            int result;
            if (va == null || vb == null)
            {
                // null 恆排最後，不受升降序影響
                result = (va == null ? 1 : 0) - (vb == null ? 1 : 0);
                return result != 0 ? result : TieBreak(a, b);
            }

            try
            {
                result = _fieldType switch
                {
                    ChartFieldType.Enum   => RankOf(va).CompareTo(RankOf(vb)),
                    ChartFieldType.Number => Convert.ToDouble(va).CompareTo(Convert.ToDouble(vb)),
                    ChartFieldType.Date   => ((DateTime)va).CompareTo((DateTime)vb),
                    _                     => string.Compare(va.ToString(), vb.ToString(), StringComparison.OrdinalIgnoreCase),
                };
            }
            catch
            {
                return TieBreak(a, b);   // 型別誤配（如欄位新增時型別填錯）視為無法比較，回退主名稱排序
            }

            if (_descending) result = -result;
            return result != 0 ? result : TieBreak(a, b);
        }

        private int RankOf(object value)
            => _rankMap.TryGetValue(value.ToString() ?? "", out var rank) ? rank : int.MaxValue;

        private static int TieBreak(ChartRowViewModel a, ChartRowViewModel b)
            => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
    }
}
