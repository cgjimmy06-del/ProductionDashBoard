using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.UiModels
{
    public partial class UserInfo : ObservableObject
    {
        public int Id { get; set; } = 2; // 預設為訪客
        public required string UserId { get; set; }
        public required string Name { get; set; }
        public string? CardId { get; set; }
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int RoleId { get; set; } = 1; // 訪客權限
        public string? DepartmentId { get; set; }

        [ObservableProperty]
        private int status;
    }
}
