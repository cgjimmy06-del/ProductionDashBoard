using System.Text.Json.Serialization;

namespace FProductionDashBoard.Dtos;

public class PartInfoDto
{
    [JsonPropertyName("bmstype")]
    public string BmsType { get; set; } = "";

    [JsonPropertyName("p_stype_en")]
    public string? ProductName { get; set; }

    // 方案①暫不使用，先對應保留供未來以 ERP 品牌取代 DB 查詢（方案②）低成本擴充
    [JsonPropertyName("custg_abmm")]
    public string? Brand { get; set; }
}
