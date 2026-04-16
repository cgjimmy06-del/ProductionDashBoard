using Azure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public class MaterialReplacementRecord
    {
        public int ReplacementId { get; set; } // PK
        public int EquipmentId { get; set; } // FK
        public int EmployeeId { get; set; } // FK
        public string ErrorCode { get; set; } = string.Empty; // FK
        public DateTime? CreateAt { get; set; }

        public Equipment? Equipment { get; set; }
        public Employee? Employee { get; set; }
        public ErrorList? Error { get; set; }
        public ICollection<MaterialReplacementDetail> ReplacementDetails { get; set; } = []; // 對應的明細
    }
    public class MaterialReplacementDetail
    {
        public int DetailId { get; set; }     // PK
        public int ReplacementId { get; set; }     // FK
        public int MaterialId { get; set; }   // FK
        public int Quantity { get; set; }

        public MaterialReplacementRecord? ReplacementRecord { get; set; }
        public Material? Material { get; set; }
    }

    public class MaterialReplacementRecordConfiguration : IEntityTypeConfiguration<MaterialReplacementRecord>
    {
        public void Configure(EntityTypeBuilder<MaterialReplacementRecord> builder)
        {
            builder.ToTable("material_replacement_record");

            builder.HasKey(t => t.ReplacementId);
            builder.Property(t => t.ReplacementId).HasColumnName("replacement_id").ValueGeneratedOnAdd();

            builder.Property(t => t.EquipmentId).HasColumnName("equipment_id");
            builder.Property(t => t.EmployeeId).HasColumnName("employee_id");
            builder.Property(t => t.ErrorCode).HasColumnName("error_code");
            builder.Property(e => e.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");

            builder.HasOne(r => r.Equipment)
                   .WithMany() // 如果 Equipment 有集合可改成 .WithMany(e => e.ReplacementRecords)
                   .HasForeignKey(r => r.EquipmentId);

            builder.HasOne(r => r.Employee)
                   .WithMany()
                   .HasForeignKey(r => r.EmployeeId);

            builder.HasOne(t => t.Error)
                   .WithMany()
                   .HasForeignKey(t => t.ErrorCode)
                   .HasPrincipalKey(e => e.ErrorCode);
        }
    }
    public class MaterialReplacementDetailConfiguration : IEntityTypeConfiguration<MaterialReplacementDetail>
    {
        public void Configure(EntityTypeBuilder<MaterialReplacementDetail> builder)
        {
            builder.ToTable("material_replacement_detail");

            builder.HasKey(t => t.DetailId);
            builder.Property(t => t.DetailId).HasColumnName("detail_id").ValueGeneratedOnAdd();

            builder.Property(t => t.ReplacementId).HasColumnName("replacement_id");
            builder.Property(t => t.MaterialId).HasColumnName("material_id");
            builder.Property(t => t.Quantity).HasColumnName("quantity");

            builder.HasOne(d => d.ReplacementRecord)
               .WithMany(r => r.ReplacementDetails)
               .HasForeignKey(d => d.ReplacementId)
               .OnDelete(DeleteBehavior.Cascade); // 當主表刪除時，明細也會跟著刪除

            builder.HasOne(d => d.Material)
               .WithMany(m => m.ReplacementDetails)
               .HasForeignKey(d => d.MaterialId)
               .OnDelete(DeleteBehavior.Restrict); // 當主表刪除時，明細也會跟著刪除
        }
    }
}
