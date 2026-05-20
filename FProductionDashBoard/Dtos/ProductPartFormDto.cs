namespace FProductionDashBoard.Dtos
{
    public class ProductPartFormDto
    {
        public int? Id { get; set; }            // null = 新增
        public string PartNo { get; set; } = ""; // varchar(8) UNIQUE
        public string? Brand { get; set; }
        public string? Name { get; set; }
    }
}
