using FProductionDashBoard.Models;

namespace FProductionDashBoard.UiModels
{
    public class OrderProductionInfo
    {
        public int OrderId { get; set; }
        public int EquipmentProductId { get; set; }
        public int SopId { get; set; }
        public int? SopProductId { get; set; }   // SopChecklist.ProductId — for inspection/tuning linkage
        public OrderProductionStatus Status { get; set; }
        public string ProductName { get; set; } = string.Empty;  // Part.PartNo + "_" + Model.Name
        public string ProcessName { get; set; } = string.Empty;  // Sop.Process.Name
        public int? Quantity { get; set; }
        public string? Description { get; set; }
        public bool IsInProduction => Status == OrderProductionStatus.InProduction;

        public static OrderProductionInfo FromEntity(OrderProduction o) => new()
        {
            OrderId = o.OrderId,
            EquipmentProductId = o.EquipmentProductId,
            SopId = o.EquipmentProduct?.SopId ?? 0,
            SopProductId = o.EquipmentProduct?.Sop?.ProductId,
            Status = o.Status,
            ProductName = o.EquipmentProduct?.Sop?.Product is { } p
                ? $"{p.Part?.PartNo}_{p.Model?.Name}"
                : string.Empty,
            ProcessName = o.EquipmentProduct?.Sop?.Process?.Name ?? string.Empty,
            Quantity = o.Quantity,
            Description = o.Description
        };
    }
}
