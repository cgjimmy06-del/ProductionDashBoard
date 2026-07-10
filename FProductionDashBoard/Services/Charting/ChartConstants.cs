namespace FProductionDashBoard.Services
{
    /// <summary>圖表功能數量上限與檔位常數（皆按需修正）</summary>
    public static class ChartConstants
    {
        public const int MaxCharts = 10;
        public const int MaxFilterFields = 3;
        public const int MaxQuickFilterButtons = 5;
        public const int MaxSortOptions = 5;
        public const int MaxChips = 3;
        public const int MaxSecondaryInfos = 3;

        /// <summary>檢視端縮放檔位（%）；存 Properties.Settings，不進 ChartDefinition</summary>
        public static readonly int[] ZoomLevels = { 75, 100, 125, 150 };
        public const int DefaultZoomPercent = 100;
    }
}
