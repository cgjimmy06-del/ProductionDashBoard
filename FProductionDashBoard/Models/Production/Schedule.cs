using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

namespace FProductionDashBoard.Models
{
    public enum ScheduleStatus
    {
        Pending   = 1,
        Scheduled = 2,
        Completed = 3,
        Released  = 4,
        Cancelled = 5
    }

    public class Schedule
    {
        public int ScheduleId { get; set; }
        public int ProductId { get; set; }
        public int ProcessId { get; set; }
        public int Quantity { get; set; }
        public string? LotNo { get; set; }
        public ScheduleStatus Status { get; set; }

        // 入料
        public int ReceivedBy { get; set; }
        public DateTime? ReceivedAt { get; set; }

        // 排單
        public int? ScheduledBy { get; set; }
        public DateTime? ScheduledAt { get; set; }

        // 完成審核
        public int? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public int? ActualQuantity { get; set; }

        // 出料
        public int? ReleasedBy { get; set; }
        public DateTime? ReleasedAt { get; set; }

        public string? Description { get; set; }
        public int? ParentId { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public Product? Product { get; set; }
        public WorkProcess? Process { get; set; }
        public Employee? ReceivedByEmployee { get; set; }
        public Employee? ScheduledByEmployee { get; set; }
        public Employee? VerifiedByEmployee { get; set; }
        public Employee? ReleasedByEmployee { get; set; }
        public Schedule? Parent { get; set; }
        public ICollection<Schedule> Children { get; set; } = new List<Schedule>();
        public ICollection<OrderProduction> OrderProductions { get; set; } = new List<OrderProduction>();
    }

    public class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
    {
        public void Configure(EntityTypeBuilder<Schedule> builder)
        {
            builder.ToTable("schedule");

            builder.HasKey(s => s.ScheduleId);
            builder.Property(s => s.ScheduleId).HasColumnName("schedule_id").ValueGeneratedOnAdd();

            builder.Property(s => s.ProductId).HasColumnName("product_id");
            builder.Property(s => s.ProcessId).HasColumnName("process_id");
            builder.Property(s => s.Quantity).HasColumnName("quantity");
            builder.Property(s => s.LotNo).HasColumnName("lot_no");
            builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>();
            builder.Property(s => s.ReceivedBy).HasColumnName("received_by");
            builder.Property(s => s.ReceivedAt).HasColumnName("received_at").HasDefaultValueSql("(sysdatetime())");
            builder.Property(s => s.ScheduledBy).HasColumnName("scheduled_by");
            builder.Property(s => s.ScheduledAt).HasColumnName("scheduled_at");
            builder.Property(s => s.VerifiedBy).HasColumnName("verified_by");
            builder.Property(s => s.VerifiedAt).HasColumnName("verified_at");
            builder.Property(s => s.ActualQuantity).HasColumnName("actual_quantity");
            builder.Property(s => s.ReleasedBy).HasColumnName("released_by");
            builder.Property(s => s.ReleasedAt).HasColumnName("released_at");
            builder.Property(s => s.Description).HasColumnName("description");
            builder.Property(s => s.ParentId).HasColumnName("parent_id");
            builder.Property(s => s.CreateAt).HasColumnName("create_at").HasDefaultValueSql("(sysdatetime())");
            builder.Property(s => s.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("(sysdatetime())");

            builder.HasOne(s => s.Product)
                   .WithMany()
                   .HasForeignKey(s => s.ProductId);

            builder.HasOne(s => s.Process)
                   .WithMany()
                   .HasForeignKey(s => s.ProcessId);

            builder.HasOne(s => s.ReceivedByEmployee)
                   .WithMany()
                   .HasForeignKey(s => s.ReceivedBy);

            builder.HasOne(s => s.ScheduledByEmployee)
                   .WithMany()
                   .HasForeignKey(s => s.ScheduledBy)
                   .IsRequired(false);

            builder.HasOne(s => s.VerifiedByEmployee)
                   .WithMany()
                   .HasForeignKey(s => s.VerifiedBy)
                   .IsRequired(false);

            builder.HasOne(s => s.ReleasedByEmployee)
                   .WithMany()
                   .HasForeignKey(s => s.ReleasedBy)
                   .IsRequired(false);

            builder.HasOne(s => s.Parent)
                   .WithMany(s => s.Children)
                   .HasForeignKey(s => s.ParentId)
                   .IsRequired(false);

            builder.HasMany(s => s.OrderProductions)
                   .WithOne(o => o.Schedule)
                   .HasForeignKey(o => o.ScheduleId)
                   .IsRequired(false);
        }
    }
}
