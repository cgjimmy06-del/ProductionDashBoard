using FProductionDashBoard.Models;
using System.Collections.Generic;

namespace FProductionDashBoard.Dtos
{
    public class SopChecklistFormDto
    {
        public int? Id { get; set; }                         // null = 新增
        public int PartId { get; set; }                       // 後端 EnsureProductAsync(PartId, ModelId)
        public int ModelId { get; set; }
        public int ProcessId { get; set; }
        public SopType SopType { get; set; }
        public string? Remark { get; set; }
        public List<SopChecklistItemFormDto> Items { get; set; } = new();
    }
}
