using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace FProductionDashBoard.Models
{
    public enum ProgramTuningStatus
    {
        InProgress = 1,
        Completed = 2,
        Cancelled = 3
    }

    public class ProgramTuningRecord
    {
        public int ProgramTuningId { get; set; }
        public int EquipmentId { get; set; }
        public int EquipmentProductId { get; set; }
        public TuningType TuningType { get; set; } // 來源: EquipmentProduct
        public ProgramTuningStatus Status { get; set; }
        public int StartedBy { get; set; }        // 實際執行人
        public int? ManagedBy { get; set; }        // 安排人（歷史列為 NULL）
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string? Description { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public Equipment? Equipment { get; set; }
        public EquipmentProduct? EquipmentProduct { get; set; }
        public Employee? StartedByEmployee { get; set; }
        public Employee? ManagedByEmployee { get; set; }
    }

    public class ProgramTuningRecordConfiguration : IEntityTypeConfiguration<ProgramTuningRecord>
    {
        public void Configure(EntityTypeBuilder<ProgramTuningRecord> builder)
        {
            builder.ToTable("program_tuning_record");

            builder.HasKey(r => r.ProgramTuningId);
            builder.Property(r => r.ProgramTuningId).HasColumnName("program_tuning_id").ValueGeneratedOnAdd();

            builder.Property(r => r.EquipmentId).HasColumnName("equipment_id");
            builder.Property(r => r.EquipmentProductId).HasColumnName("equipment_product_id");
            builder.Property(r => r.TuningType).HasColumnName("tuning_type").HasConversion<string>();
            builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>();
            builder.Property(r => r.StartedBy).HasColumnName("started_by");
            builder.Property(r => r.ManagedBy).HasColumnName("managed_by");
            builder.Property(r => r.StartedAt).HasColumnName("started_at");
            builder.Property(r => r.EndedAt).HasColumnName("ended_at");
            builder.Property(r => r.Description).HasColumnName("description");
            builder.Property(r => r.CreateAt).HasColumnName("create_at").HasDefaultValueSql("sysdatetime()");
            builder.Property(r => r.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("sysdatetime()");

            builder.HasOne(r => r.Equipment)
                   .WithMany()
                   .HasForeignKey(r => r.EquipmentId);

            builder.HasOne(r => r.EquipmentProduct)
                   .WithMany()
                   .HasForeignKey(r => r.EquipmentProductId);

            builder.HasOne(r => r.StartedByEmployee)
                   .WithMany()
                   .HasForeignKey(r => r.StartedBy);

            builder.HasOne(r => r.ManagedByEmployee)
                   .WithMany()
                   .HasForeignKey(r => r.ManagedBy);
        }
    }
}
