namespace FProductionDashBoard.Dtos
{
    public class EmployeeFormDto
    {
        public int? Id { get; set; }
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Password { get; set; } = "";   // 空字串 = 編輯時不更新密碼
        public int RoleId { get; set; } = 1;
        public string? CardId { get; set; }
        public string? Email { get; set; }
        public string? DepartmentId { get; set; }
    }
}
