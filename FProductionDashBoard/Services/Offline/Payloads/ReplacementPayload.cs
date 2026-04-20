namespace FProductionDashBoard.Services.Offline.Payloads
{
    public class ReplacementPayload
    {
        public int EquipmentId { get; set; }
        public int EmployeeId { get; set; }
        public List<MaterialItem> Materials { get; set; } = new();
        public DateTime OperatedAt { get; set; } = DateTime.Now;
    }
}
