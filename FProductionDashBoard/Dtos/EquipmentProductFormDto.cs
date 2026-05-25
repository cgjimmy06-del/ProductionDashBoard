namespace FProductionDashBoard.Dtos
{
    public class EquipmentProductFormDto
    {
        public int? Id { get; set; }         // null = 新增
        public int EquipmentId { get; set; }
        public int SopId { get; set; }
        public int SeqNo { get; set; }
    }
}
