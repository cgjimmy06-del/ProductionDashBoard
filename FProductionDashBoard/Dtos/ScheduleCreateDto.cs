namespace FProductionDashBoard.Dtos
{
    public class ScheduleCreateDto
    {
        public int ProductId { get; set; }
        public int ProcessId { get; set; }
        public int Quantity { get; set; }
        public string? LotNo { get; set; }
        public string? Description { get; set; }
        public int ReceivedBy { get; set; }
    }
}
