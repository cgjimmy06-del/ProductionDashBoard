using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public enum InspectionType
    {
        First = 1,   // 首件檢驗
        Routine = 2  // 巡檢
    }
    public class InspectionRecord
    {
        public int InspectionId { get; set; } // PK
        public int EquipmentId { get; set; } // FK
        public int EmployeeId { get; set; } // FK
        public InspectionType InspectionType { get; set; } // ENUM
        public bool Result { get; set; } // true=合格, false=不合格
        public int? ProductId { get; set; }
        public int? TimeSlotId { get; set; } // FK (巡檢用)
        public string? ErrorCode { get; set; } // FK
        public string? Description { get; set; }
        public DateTime? CreateAt { get; set; }

        public Equipment? Equipment { get; set; }
        public Employee? Employee { get; set; }
        public ErrorList? Error { get; set; }
        public TimeSlotLookup? TimeSlot { get; set; }
        public Product? Product { get; set; }
    }
    public class InspectionRecordConfiguration : IEntityTypeConfiguration<InspectionRecord>
    {
        public void Configure(EntityTypeBuilder<InspectionRecord> builder)
        {
            builder.ToTable("inspection_record");

            builder.HasKey(t => t.InspectionId);
            builder.Property(t => t.InspectionId).HasColumnName("inspection_id").ValueGeneratedOnAdd();

            builder.Property(t => t.EquipmentId).HasColumnName("equipment_id");
            builder.Property(t => t.EmployeeId).HasColumnName("employee_id");
            builder.Property(t => t.InspectionType).HasColumnName("inspection_type").HasConversion<string>();
            builder.Property(t => t.Result).HasColumnName("result");
            builder.Property(t => t.ProductId).HasColumnName("product_id");
            builder.Property(t => t.TimeSlotId).HasColumnName("timeslot_id");
            builder.Property(t => t.ErrorCode).HasColumnName("error_code");
            builder.Property(t => t.Description).HasColumnName("description");
            builder.Property(e => e.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");

            builder.HasOne(r => r.Equipment)
                   .WithMany()
                   .HasForeignKey(r => r.EquipmentId);

            builder.HasOne(r => r.Employee)
                   .WithMany()
                   .HasForeignKey(r => r.EmployeeId);

            builder.HasOne(t => t.Error)
                   .WithMany()
                   .HasForeignKey(t => t.ErrorCode)
                   .HasPrincipalKey(e => e.ErrorCode);

            builder.HasOne(t => t.TimeSlot)
                   .WithMany(e => e.InspectionRecords)
                   .HasForeignKey(t => t.TimeSlotId);

            builder.HasOne(r => r.Product)
                   .WithMany()
                   .HasForeignKey(r => r.ProductId)
                   .IsRequired(false);
        }
    }
}
