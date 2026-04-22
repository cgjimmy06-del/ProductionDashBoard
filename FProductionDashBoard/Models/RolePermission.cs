using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public class Role
    {
        public int RoleId { get; set; } // PK
        public string? Name { get; set; }
        public string? Description { get; set; }
        
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
    public class Permission
    {
        public int PermissionId { get; set; } // PK
        public string? Name { get; set; }
        public string? Remark { get; set; }

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
    public class RolePermission
    {
        public int RoleId { get; set; } // FK
        public int PermissionId { get; set; } // FK
        public DateTime? CreateAt { get; set; }

        public Role? Role { get; set; } // 導覽屬性
        public Permission? Permission { get; set; } // 導覽屬性
    }

    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("role");
            // 主鍵
            builder.HasKey(e => e.RoleId);
            builder.Property(e => e.RoleId).HasColumnName("role_id");

            builder.Property(e => e.Name).HasColumnName("name");
            builder.Property(e => e.Description).HasColumnName("description");

            builder.HasMany(r => r.RolePermissions)
                .WithOne(rp => rp.Role)
                .HasForeignKey(rp => rp.RoleId);
        }
    }
    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("permission");
            // 主鍵
            builder.HasKey(e => e.PermissionId);
            builder.Property(e => e.PermissionId).HasColumnName("permission_id");

            builder.Property(e => e.Name).HasColumnName("name");
            builder.Property(e => e.Remark).HasColumnName("remark");

            builder.HasMany(p => p.RolePermissions)
                .WithOne(rp => rp.Permission)
                .HasForeignKey(rp => rp.PermissionId);
        }
    }
    public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
    {
        public void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            builder.ToTable("role_permission");
            // 主鍵
            builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            builder.Property(e => e.RoleId).HasColumnName("role_id");
            builder.Property(e => e.PermissionId).HasColumnName("permission_id");

            builder.Property(e => e.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");
        }
    }
}
