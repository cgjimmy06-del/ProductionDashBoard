namespace FProductionDashBoard.Dtos
{
    public class ScheduleStatusUpdateDto
    {
        public int ScheduleId { get; set; }
        public int EmployeeId { get; set; }
        public int? ActualQuantity { get; set; }
        public string? Description { get; set; }
    }
}
