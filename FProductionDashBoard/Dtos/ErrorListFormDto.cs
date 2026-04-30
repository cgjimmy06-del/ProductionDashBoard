namespace FProductionDashBoard.Dtos
{
    public class ErrorListFormDto
    {
        public int? Id { get; set; }
        public string ErrorCode { get; set; } = "";
        public int? TypeId { get; set; }
        public int Severity { get; set; } = 0;
        public string? MessageZhTw { get; set; }
        public string? MessageEnUs { get; set; }
        public string? MessageViVn { get; set; }
    }
}
