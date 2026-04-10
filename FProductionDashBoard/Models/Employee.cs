using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public class Employee
    {
        public int Id { get; set; } // 代理鍵 主鍵
        public string UserId { get; set; } = string.Empty;
        public string? CardId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int Permission { get; set; }
        public string? DepartmentId { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
    }

    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("employee");
            // 主鍵
            builder.HasKey(e => e.Id);
            // 代理鍵 (資料庫已設定自動加一)
            builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            // 只需定義欄位名稱，型態/長度由資料庫決定
            builder.Property(e => e.UserId).HasColumnName("user_id");
            builder.Property(e => e.CardId).HasColumnName("card_id");
            builder.Property(e => e.Name).HasColumnName("name");
            builder.Property(e => e.Password).HasColumnName("password");
            builder.Property(e => e.Email).HasColumnName("email");
            builder.Property(e => e.Permission).HasColumnName("permission");
            builder.Property(e => e.DepartmentId).HasColumnName("department_id");
            builder.Property(e => e.CreateTime).HasColumnName("createTime");
            builder.Property(e => e.UpdateTime).HasColumnName("updateTime");
        }
    }
}
