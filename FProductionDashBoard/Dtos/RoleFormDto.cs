using System.Collections.Generic;

namespace FProductionDashBoard.Dtos
{
    public class RoleFormDto
    {
        public int? Id { get; set; }
        public int RoleId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public List<int> SelectedPermissionIds { get; set; } = new();
    }
}
