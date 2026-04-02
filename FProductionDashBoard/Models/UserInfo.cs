using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
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
        public required string ID { get; set; }   // 主鍵
        public required string Name { get; set; }
        public string Password { get; set; } = "0000";
        public string? Email { get; set; }
        public int Role { get; set; } = (int)Roles.Viewer;
        public string? DepartmentId { get; set; }

        [ObservableProperty]
        private int status;
        //public ICollection<DeviceInfo> Devices { get; set; } = new List<DeviceInfo>();
    }

    public class UserInfoConfiguration : IEntityTypeConfiguration<UserInfo>
    {
        public void Configure(EntityTypeBuilder<UserInfo> builder)
        {
            builder.ToTable("employee");
            builder.HasKey(d => d.ID);
            builder.Property(d => d.ID).HasColumnName("user_id");
            builder.Property(d => d.Name).HasColumnName("name");
            builder.Property(d => d.Password).HasColumnName("password");
            builder.Property(d => d.Email).HasColumnName("email");
            builder.Property(d => d.Role).HasColumnName("permission");
            builder.Property(d => d.DepartmentId).HasColumnName("department_id");

            // 忽略額外屬性
            builder.Ignore(d => d.Status);


        }
    }
}
