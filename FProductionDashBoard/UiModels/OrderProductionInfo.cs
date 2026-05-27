using FProductionDashBoard.Models;

namespace FProductionDashBoard.UiModels
{
    public class OrderProductionInfo
    {
        public int OrderId { get; set; }
        public int EquipmentProductId { get; set; }
        public int SopId { get; set; }
        public OrderProductionStatus Status { get; set; }
        public string ProductName { get; set; } = string.Empty;  // Part.PartNo + "_" + Model.Name
        public string ProcessName { get; set; } = string.Empty;  // Sop.Process.Name
        public int? Quantity { get; set; }
        public string? Description { get; set; }
        public bool IsInProduction => Status == OrderProductionStatus.InProduction;
    }
}
