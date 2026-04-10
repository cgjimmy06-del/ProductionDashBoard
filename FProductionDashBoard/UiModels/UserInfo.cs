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
        Viewer = 0,
        Operator = 1,
        Supervisor = 2,
        Admin = 3
    }

    public partial class UserInfo : ObservableObject
    {
        public required string ID { get; set; }
        public required string Name { get; set; }
        public string Password { get; set; } = "0000";
        public string? Email { get; set; }
        public int Role { get; set; } = (int)Roles.Viewer;
        public string? DepartmentId { get; set; }

        [ObservableProperty]
        private int status;
        //public ICollection<DeviceInfo> Devices { get; set; } = new List<DeviceInfo>();
    }
}
