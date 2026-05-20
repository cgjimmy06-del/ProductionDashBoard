using FProductionDashBoard.Models;

namespace FProductionDashBoard.Dtos
{
    public class SopChecklistItemFormDto
    {
        public int? Id { get; set; }              // null = 新增
        public int Seq { get; set; }
        public CheckType CheckType { get; set; }
        public int? WorkstationNo { get; set; }   // Station 用，1~6
        public int? MaterialId { get; set; }      // Station / Fixture 用
        public string? MaterialName { get; set; } // 顯示用，不寫入 DB
        public int? Quantity { get; set; }        // Quantity 用
        public string? Content { get; set; }      // Other 用
        public string? Remark { get; set; }       // 所有類型皆顯示
    }
}
