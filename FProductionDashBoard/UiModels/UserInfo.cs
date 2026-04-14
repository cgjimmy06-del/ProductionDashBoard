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
    public enum Roles
    {
        None = -1,
        Admin = 0,
        Viewer = 1,
        Supervisor = 2,
        Operator = 3 // 後續加入不同職責的操作員代碼
    }

    public partial class UserInfo : ObservableObject
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public required string Name { get; set; }
        public string? CardId { get; set; }
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int Role { get; set; } = (int)Roles.Viewer;
        public string? DepartmentId { get; set; }

        [ObservableProperty]
        private int status;
        //public ICollection<DeviceInfo> Devices { get; set; } = new List<DeviceInfo>();
    }
}
