using FProductionDashBoard.Models;

namespace FProductionDashBoard.UiModels
{
    /// <summary>
    /// 倉位設定清單列的顯示模型：包裝來源 <see cref="StorageLocation"/> 實體，
    /// 另帶查詢導出的現況箱數與機邊設備名稱。列於載入時整批重建，不需 ObservableObject。
    /// 保留 <see cref="Source"/> 供編輯/更新時回填未在表單顯示的預留欄位（AmrStationCode/MapX/MapY）。
    /// </summary>
    public class StorageLocationRow
    {
        public StorageLocation Source { get; }
        public int OccupancyCount { get; }
        public string? EquipmentName { get; }

        public StorageLocationRow(StorageLocation source, int occupancyCount, string? equipmentName)
        {
            Source = source;
            OccupancyCount = occupancyCount;
            EquipmentName = equipmentName;
        }

        // DataGrid 直接綁定用（轉發 Source 純量欄位，避免 XAML 寫 Source.Xxx）
        public string Code => Source.Code;
        public string? Zone => Source.Zone;
        public LocationType LocationType => Source.LocationType;
        public int? Capacity => Source.Capacity;
        public bool IsEnabled => Source.IsEnabled;
    }
}
