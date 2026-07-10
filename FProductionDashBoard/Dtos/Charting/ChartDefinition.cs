using System.Collections.Generic;

namespace FProductionDashBoard.Dtos
{
    /// <summary>
    /// 圖表頁的宣告式定義（可序列化存 JSON）。
    /// 三層：基本 / 統計列 / 篩選列 / 容器；欄位一律以 FieldId 參照 FieldCatalog。
    /// 標籤類欄位採「LabelKey 優先、Label 備用」：預設圖表填資源 key 保持多語言，
    /// 使用者自訂圖表（Stage 3 設計器）填 Label 純文字。
    /// </summary>
    public class ChartDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? NameKey { get; set; }
        public ChartDataSet DataSet { get; set; } = ChartDataSet.Equipment;
        public int SortOrder { get; set; }
        /// <summary>預設圖表：鎖定不可編輯/刪除，僅可複製（確保現場永遠有畫面）</summary>
        public bool IsDefault { get; set; }

        public StatRowConfig StatRow { get; set; } = new();
        public FilterRowConfig FilterRow { get; set; } = new();
        public ContainerConfig Container { get; set; } = new();
    }

    public class StatRowConfig
    {
        public bool Enabled { get; set; } = true;
        public List<StatItemConfig> Items { get; set; } = new();
    }

    public class StatItemConfig
    {
        public ChartAggregateType Aggregate { get; set; } = ChartAggregateType.Count;
        /// <summary>聚合欄位；Count 且未指定 FilterValue 時可為空字串（＝全部列計數）</summary>
        public string FieldId { get; set; } = "";
        /// <summary>選填：先過濾「FieldId＝此值」再聚合（如 生產狀態=生產中 的計數）</summary>
        public string? FilterValue { get; set; }
        public string Label { get; set; } = "";
        public string? LabelKey { get; set; }
        /// <summary>數字顏色 Brush key（選填，如 PrimaryBrush；null＝TextPrimaryBrush）</summary>
        public string? ColorKey { get; set; }
    }

    public class FilterRowConfig
    {
        public bool Enabled { get; set; } = true;
        /// <summary>篩選欄位（上限 ChartConstants.MaxFilterFields；Date 欄位起訖算 1 個）</summary>
        public List<string> FilterFieldIds { get; set; } = new();
        /// <summary>快捷按鈕（上限 ChartConstants.MaxQuickFilterButtons）</summary>
        public List<QuickFilterButtonConfig> QuickButtons { get; set; } = new();
        /// <summary>排序選項（上限 ChartConstants.MaxSortOptions；單鍵排序，同時生效 1 組）</summary>
        public List<string> SortOptionFieldIds { get; set; } = new();
        public string DefaultSortFieldId { get; set; } = "";
        public ChartSortDirection DefaultSortDirection { get; set; } = ChartSortDirection.Ascending;
    }

    /// <summary>快捷按鈕＝欄位＋固定值；AND 疊加、再按取消、同欄位互斥（後按覆蓋）</summary>
    public class QuickFilterButtonConfig
    {
        public string FieldId { get; set; } = "";
        public string Value { get; set; } = "";
        /// <summary>選填自訂標籤；Label 與 LabelKey 皆空時以值標籤（FieldCatalog）顯示</summary>
        public string Label { get; set; } = "";
        public string? LabelKey { get; set; }
    }

    public class ContainerConfig
    {
        public ChartContainerType Type { get; set; } = ChartContainerType.Card;
        public CardContainerConfig Card { get; set; } = new();
        public TableContainerConfig Table { get; set; } = new();
    }

    public class CardContainerConfig
    {
        /// <summary>邊框色來源欄位（Enum 型，含標題列右側狀態色點；色值由 FieldCatalog 值宣告）</summary>
        public string BorderColorFieldId { get; set; } = "";
        public string TitleFieldId { get; set; } = "";
        /// <summary>chip 於主名稱下方獨立列 wrap（上限 ChartConstants.MaxChips）</summary>
        public List<ChipConfig> Chips { get; set; } = new();
        /// <summary>次要資訊每列一欄位（上限 ChartConstants.MaxSecondaryInfos）</summary>
        public List<string> SecondaryFieldIds { get; set; } = new();
        /// <summary>進度條分子/分母欄位；任一為 null 則不顯示進度條</summary>
        public string? ProgressNumeratorFieldId { get; set; }
        public string? ProgressDenominatorFieldId { get; set; }
    }

    /// <summary>chip 顏色依 FieldCatalog 值宣告的 BrushKey（依值上色）</summary>
    public class ChipConfig
    {
        public string FieldId { get; set; } = "";
        public bool ShowOnlyWhenHasValue { get; set; } = true;
    }

    public class TableContainerConfig
    {
        /// <summary>表格欄位（有序）；排序統一由 FilterRowConfig 的排序設定控制</summary>
        public List<string> ColumnFieldIds { get; set; } = new();
        /// <summary>燈號來源欄位（Enum 型，首欄色點；null＝不顯示燈號欄）</summary>
        public string? IndicatorFieldId { get; set; }
    }
}
