namespace FProductionDashBoard.Dtos
{
    public class ScheduleSplitDto
    {
        public int OriginalScheduleId { get; set; }
        public int RemainingQuantity { get; set; }
        public int ReleasedBy { get; set; }
        public string? Description { get; set; }
    }
}
