using System.Collections.Generic;

namespace FProductionDashBoard.UiModels
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
}
