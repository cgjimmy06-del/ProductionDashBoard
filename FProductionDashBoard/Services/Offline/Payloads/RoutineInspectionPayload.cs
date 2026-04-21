namespace FProductionDashBoard.Services.Offline.Payloads
{
    public class RoutineInspectionPayload
    {
        public int EquipmentId { get; set; }
        public int EmployeeId { get; set; }
        public bool Result { get; set; }
        public int TimeSlotId { get; set; }
        public string? Product { get; set; }
        public string? ErrorCode { get; set; }
        public string? Description { get; set; }
        public DateTime OperatedAt { get; set; } = DateTime.Now;
    }
}
