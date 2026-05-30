namespace FProductionDashBoard.Models.Extra
{
    // Dapper POCO，不需要 IEntityTypeConfiguration
    public class VwMesDailyProcessData
    {
        public string DeviceId { get; set; } = string.Empty;    // 欄位：DeviceID
        public DateTime UpdateDateTime { get; set; }             // 欄位：UpdateDateTime (datetime2)
        public double Collection4 { get; set; }                  // 欄位：Collection4 (float)
        public string CollectionS6 { get; set; } = string.Empty;
        public string CollectionS7 { get; set; } = string.Empty;
        public string CollectionS8 { get; set; } = string.Empty;
        public string CollectionS9 { get; set; } = string.Empty;
    }
}
