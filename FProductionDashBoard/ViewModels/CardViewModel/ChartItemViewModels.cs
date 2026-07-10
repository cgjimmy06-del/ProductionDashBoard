using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FProductionDashBoard.ViewModels
{
    /// <summary>
    /// 圖表容器的泛用資料列：Values 存原始值（排序/篩選/統計用）、
    /// Display 存格式化字串（表格 cell 與次要資訊用）、其餘為卡片槽位（依定義預先計算）。
    /// 列於載入時整批重建，不需 ObservableObject。
    /// </summary>
    public class ChartRowViewModel
    {
        public Dictionary<string, object?> Values { get; } = new();
        public Dictionary<string, string> Display { get; } = new();

        // 卡片槽位
        public string Title { get; set; } = "";
        public string BorderBrushKey { get; set; } = "BorderBrush";
        public List<ChartChipItem> Chips { get; } = new();
        public List<ChartSecondaryItem> SecondaryInfos { get; } = new();
        public bool HasProgress { get; set; }
        public double ProgressPercent { get; set; }
        public string ProgressText { get; set; } = "";

        // 表格燈號
        public string IndicatorBrushKey { get; set; } = "IdleBrush";
    }

    public record ChartChipItem(string Text, string BrushKey);

    public record ChartSecondaryItem(string Label, string Text);

    public record ChartStatItemViewModel(string Label, string Value, string ColorKey);

    public record ChartTableColumn(string FieldId, string Header);

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

            result = _fieldType switch
            {
                ChartFieldType.Enum   => RankOf(va).CompareTo(RankOf(vb)),
                ChartFieldType.Number => Convert.ToDouble(va).CompareTo(Convert.ToDouble(vb)),
                ChartFieldType.Date   => ((DateTime)va).CompareTo((DateTime)vb),
                _                     => string.Compare(va.ToString(), vb.ToString(), StringComparison.OrdinalIgnoreCase),
            };

            if (_descending) result = -result;
            return result != 0 ? result : TieBreak(a, b);
        }

        private int RankOf(object value)
            => _rankMap.TryGetValue(value.ToString() ?? "", out var rank) ? rank : int.MaxValue;

        private static int TieBreak(ChartRowViewModel a, ChartRowViewModel b)
            => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
    }
}
