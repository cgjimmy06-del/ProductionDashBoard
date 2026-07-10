namespace FProductionDashBoard.Dtos
{
    /// <summary>圖表主資料集（視角）：一張卡/一列代表什麼</summary>
    public enum ChartDataSet
    {
        Equipment = 1,  // 設備視角
        Schedule  = 2   // 排程視角
    }

    /// <summary>容器類型：新增類型＝加一組 enum + Config，不動既有程式</summary>
    public enum ChartContainerType
    {
        Card  = 1,  // 卡片牆
        Table = 2,  // 表格
        Map   = 3,  // 地圖式（預留，未實作）
        Graph = 4   // 統計圖（預留，未實作）
    }

    /// <summary>統計列聚合方式</summary>
    public enum ChartAggregateType
    {
        Count = 1,  // 計數
        Sum   = 2   // 加總（Number 欄位）
    }

    /// <summary>欄位型別：決定篩選控件形態（Enum→下拉、Text→關鍵字、Date→起訖區間）</summary>
    public enum ChartFieldType
    {
        Text   = 1,
        Enum   = 2,
        Number = 3,
        Date   = 4
    }

    public enum ChartSortDirection
    {
        Ascending  = 1,
        Descending = 2
    }
}
