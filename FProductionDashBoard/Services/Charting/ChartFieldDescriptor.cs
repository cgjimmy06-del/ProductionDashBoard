using System.Collections.Generic;
using FProductionDashBoard.Dtos;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 欄位目錄的單一欄位宣告：編輯器所有下拉選項與檢視端渲染皆由此驅動，
    /// 新增欄位＝在 FieldCatalog 加一筆宣告（單點可修改）。
    /// </summary>
    public class ChartFieldDescriptor
    {
        public ChartFieldDescriptor(
            string fieldId, string labelKey, ChartFieldType type,
            bool canStat = false, bool canFilter = false,
            bool canQuickFilter = false, bool canSort = false,
            IReadOnlyList<ChartFieldValue>? values = null)
        {
            FieldId = fieldId;
            LabelKey = labelKey;
            Type = type;
            CanStat = canStat;
            CanFilter = canFilter;
            CanQuickFilter = canQuickFilter;
            CanSort = canSort;
            Values = values ?? new List<ChartFieldValue>();
        }

        public string FieldId { get; }
        /// <summary>標籤的 .resx 資源 key（程式端取用，非 StrResources）</summary>
        public string LabelKey { get; }
        public ChartFieldType Type { get; }
        public bool CanStat { get; }
        public bool CanFilter { get; }
        /// <summary>可作快捷按鈕（僅 Enum 型欄位）</summary>
        public bool CanQuickFilter { get; }
        public bool CanSort { get; }
        /// <summary>Enum 型欄位的值宣告（含排序權重與狀態色）；其餘型別為空</summary>
        public IReadOnlyList<ChartFieldValue> Values { get; }
    }

    /// <summary>Enum 型欄位的單一值宣告</summary>
    public class ChartFieldValue
    {
        public ChartFieldValue(string value, string labelKey, int rank, string? brushKey = null)
        {
            Value = value;
            LabelKey = labelKey;
            Rank = rank;
            BrushKey = brushKey;
        }

        /// <summary>序列化/比對用的值字串（enum 名稱）</summary>
        public string Value { get; }
        public string LabelKey { get; }
        /// <summary>排序權重：Enum 欄位排序依此值，不按字串排</summary>
        public int Rank { get; }
        /// <summary>狀態色 Brush key（邊框/色點/chip 用；null＝IdleBrush）</summary>
        public string? BrushKey { get; }
    }
}
