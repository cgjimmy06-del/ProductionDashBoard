namespace FProductionDashBoard.Services.Offline.Payloads
{
    public class FirstInspectionPayload
    {
        public int EquipmentId { get; set; }
        public int EmployeeId { get; set; }
        public bool Result { get; set; }
        public string? Product { get; set; }
        public string? ErrorCode { get; set; }
        public string? Description { get; set; }
    }
}
