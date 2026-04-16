using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public class Equipment
    {
        public int Id { get; set; } // 代理PK
        public string Code { get; set; } = string.Empty; // UNIQUE
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; } = 0;
        public string? Factory { get; set; }
        public string? Building { get; set; }
        public string? Floor { get; set; }
        public int? TypeId { get; set; } // FK
        public string? DepartmentId { get; set; }
        public string? Description { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public EquipmentType? Type { get; set; } // 導覽屬性
    }
    public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
    {
        public void Configure(EntityTypeBuilder<Equipment> builder)
        {
            builder.ToTable("equipment");
            // 主鍵
            builder.HasKey(e => e.Id);
            // 代理鍵 (資料庫已設定自動加一)
            builder.Property(e => e.Id).HasColumnName("equipment_id").ValueGeneratedOnAdd();
            // 只需定義欄位名稱，型態/長度由資料庫決定
            builder.Property(e => e.Code).HasColumnName("equipment_code");
            builder.Property(e => e.Name).HasColumnName("name");
            builder.Property(e => e.Ip).HasColumnName("ip");
            builder.Property(e => e.Port).HasColumnName("port");
            builder.Property(e => e.Factory).HasColumnName("factory");
            builder.Property(e => e.Building).HasColumnName("building");
            builder.Property(e => e.Floor).HasColumnName("floor");
            builder.Property(e => e.TypeId).HasColumnName("type_id");
            builder.Property(e => e.DepartmentId).HasColumnName("department_id");
            builder.Property(e => e.Description).HasColumnName("description");
            builder.Property(e => e.CreateAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()"); ;
            builder.Property(e => e.UpdateAt).HasColumnName("updated_at").HasDefaultValueSql("GETDATE()"); ;

            // 設定 關聯：Type (1) ↔ Equipments (多)
            builder.HasOne(m => m.Type)
                   .WithMany(t => t.Equipments)
                   .HasForeignKey(m => m.TypeId);

            // 忽略 額外屬性
            //builder.Ignore(d => d.Status);
            // 設定 索引
            //builder.HasIndex(e => e.EquipmentId).IsUnique();
            // 設定 Not Null
            //builder.Property(e => e.EquipmentId).IsRequired();
            // 設定 預設值
            //HasColumnName("UpdateTime").HasDefaultValueSql("GETDATE()");
        }
    }
}
