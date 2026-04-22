namespace FProductionDashBoard.Dtos
{
    public class MaterialFormDto
    {
        public int? Id { get; set; }
        public string MaterialCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Brand { get; set; }
        public string? Specification { get; set; }
        public int? TypeId { get; set; }
        public string? Description { get; set; }
        public int MinimumStock { get; set; } = 0;
        public int QuantityInStock { get; set; } = 0;
    }
}
