using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace FProductionDashBoard.Models
{
    public class LocationAssignment
    {
        public int AssignmentId { get; set; } // 代理PK
        public int LocationId { get; set; }   // FK→storage_location
        public int ScheduleId { get; set; }   // FK→schedule（1 Schedule = 1 箱）
        public int AssignedBy { get; set; }   // 上架人 FK→employee
        public DateTime? AssignedAt { get; set; }
        public int? ReleasedBy { get; set; }  // 下架人 FK→employee
        public DateTime? ReleasedAt { get; set; } // NULL = 現役在庫

        public StorageLocation? Location { get; set; }
        public Schedule? Schedule { get; set; }
    }

    public class LocationAssignmentConfiguration : IEntityTypeConfiguration<LocationAssignment>
    {
        public void Configure(EntityTypeBuilder<LocationAssignment> builder)
        {
            builder.ToTable("location_assignment");

            builder.HasKey(a => a.AssignmentId);
            builder.Property(a => a.AssignmentId).HasColumnName("assignment_id").ValueGeneratedOnAdd();

            builder.Property(a => a.LocationId).HasColumnName("location_id");
            builder.Property(a => a.ScheduleId).HasColumnName("schedule_id");
            builder.Property(a => a.AssignedBy).HasColumnName("assigned_by");
            builder.Property(a => a.AssignedAt).HasColumnName("assigned_at").HasDefaultValueSql("(sysdatetime())");
            builder.Property(a => a.ReleasedBy).HasColumnName("released_by");
            builder.Property(a => a.ReleasedAt).HasColumnName("released_at");

            builder.HasOne(a => a.Location)
                   .WithMany()
                   .HasForeignKey(a => a.LocationId);

            builder.HasOne(a => a.Schedule)
                   .WithMany()
                   .HasForeignKey(a => a.ScheduleId);

            // 同一 schedule 僅允許一筆現役佔用（released_at IS NULL）；部分唯一索引，對應 V010 的 filtered unique index
            builder.HasIndex(a => a.ScheduleId)
                   .IsUnique()
                   .HasFilter("[released_at] IS NULL");
        }
    }
}
