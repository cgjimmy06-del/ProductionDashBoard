namespace FProductionDashBoard.Dtos
{
    public class EquipmentFormDto
    {
        public int? Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Ip { get; set; } = "";
        public int Port { get; set; }
        public int? TypeId { get; set; }
        public string? Factory { get; set; }
        public string? Building { get; set; }
        public string? Floor { get; set; }
        public string? DepartmentId { get; set; }
        public string? Description { get; set; }
    }
}
