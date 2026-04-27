using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public enum TuningType
    {
        Teaching = 1,   // 首件檢驗
        Offset = 2  // 巡檢
    }
    public class TuningRecord
    {
        public int TuningId { get; set; } // PK
        public int EquipmentId { get; set; } // FK
        public int EmployeeId { get; set; } // FK
        public TuningType TuningType { get; set; } // ENUM
        public int DurationSec { get; set; }
        public string? Product { get; set; }
        public DateTime? CreateAt { get; set; }

        public Equipment? Equipment { get; set; }
        public Employee? Employee { get; set; }
    }
    public class TuningRecordConfiguration : IEntityTypeConfiguration<TuningRecord>
    {
        public void Configure(EntityTypeBuilder<TuningRecord> builder)
        {
            builder.ToTable("tuning_record");

            builder.HasKey(t => t.TuningId);
            builder.Property(t => t.TuningId).HasColumnName("tuning_id").ValueGeneratedOnAdd();

            builder.Property(t => t.EquipmentId).HasColumnName("equipment_id");
            builder.Property(t => t.EmployeeId).HasColumnName("employee_id");
            builder.Property(t => t.TuningType).HasColumnName("tuning_type").HasConversion<string>();
            builder.Property(t => t.DurationSec).HasColumnName("duration_sec");
            builder.Property(t => t.Product).HasColumnName("product");
            builder.Property(e => e.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");

            builder.HasOne(r => r.Equipment)
                   .WithMany()
                   .HasForeignKey(r => r.EquipmentId);

            builder.HasOne(r => r.Employee)
                   .WithMany()
                   .HasForeignKey(r => r.EmployeeId);
        }
    }



}
