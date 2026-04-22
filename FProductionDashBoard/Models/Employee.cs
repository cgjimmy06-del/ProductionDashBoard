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
        public int EmployeeId { get; set; } // PK
        public string UserId { get; set; } = string.Empty;
        public string? CardId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int RoleId { get; set; }
        public string? DepartmentId { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public Role? Role { get; set; } // 導覽屬性
    }
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("employee");

            builder.HasKey(e => e.EmployeeId);
            builder.Property(e => e.EmployeeId).HasColumnName("employee_id").ValueGeneratedOnAdd();

            builder.Property(e => e.UserId).HasColumnName("user_id");
            builder.Property(e => e.CardId).HasColumnName("card_id");
            builder.Property(e => e.Name).HasColumnName("name");
            builder.Property(e => e.Password).HasColumnName("password");
            builder.Property(e => e.Email).HasColumnName("email");
            builder.Property(e => e.RoleId).HasColumnName("role_id");
            builder.Property(e => e.DepartmentId).HasColumnName("department_id");
            builder.Property(e => e.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()"); ;
            builder.Property(e => e.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()"); ;

            builder.HasOne(m => m.Role)
                   .WithMany(t => t.Employees)
                   .HasForeignKey(m => m.RoleId);
        }
    }
}
