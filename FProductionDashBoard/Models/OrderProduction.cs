using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace FProductionDashBoard.Models
{
    public enum OrderProductionStatus
    {
        Pending = 1,
        InProduction = 2,
        Completed = 3,
        Cancelled = 4
    }

    public class OrderProduction
    {
        public int OrderId { get; set; }
        public int EquipmentId { get; set; }
        public int EquipmentProductId { get; set; }
        public OrderProductionStatus Status { get; set; }
        public int? Quantity { get; set; }
        public int CreatedBy { get; set; }
        public int? StartedBy { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int? ScheduleId { get; set; }
        public string? Description { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public Equipment? Equipment { get; set; }
        public EquipmentProduct? EquipmentProduct { get; set; }
        public Employee? CreatedByEmployee { get; set; }
        public Employee? StartedByEmployee { get; set; }
    }

    public class OrderProductionConfiguration : IEntityTypeConfiguration<OrderProduction>
    {
        public void Configure(EntityTypeBuilder<OrderProduction> builder)
        {
            builder.ToTable("order_production");

            builder.HasKey(o => o.OrderId);
            builder.Property(o => o.OrderId).HasColumnName("order_id").ValueGeneratedOnAdd();

            builder.Property(o => o.EquipmentId).HasColumnName("equipment_id");
            builder.Property(o => o.EquipmentProductId).HasColumnName("equipment_product_id");
            builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>();
            builder.Property(o => o.Quantity).HasColumnName("quantity");
            builder.Property(o => o.CreatedBy).HasColumnName("created_by");
            builder.Property(o => o.StartedBy).HasColumnName("started_by");
            builder.Property(o => o.StartedAt).HasColumnName("started_at");
            builder.Property(o => o.EndedAt).HasColumnName("ended_at");
            builder.Property(o => o.ScheduleId).HasColumnName("schedule_id");
            builder.Property(o => o.Description).HasColumnName("description");
            builder.Property(o => o.CreateAt).HasColumnName("create_at").HasDefaultValueSql("(sysdatetime())");
            builder.Property(o => o.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("(sysdatetime())");

            builder.HasOne(o => o.Equipment)
                   .WithMany()
                   .HasForeignKey(o => o.EquipmentId);

            builder.HasOne(o => o.EquipmentProduct)
                   .WithMany()
                   .HasForeignKey(o => o.EquipmentProductId);

            builder.HasOne(o => o.CreatedByEmployee)
                   .WithMany()
                   .HasForeignKey(o => o.CreatedBy);

            builder.HasOne(o => o.StartedByEmployee)
                   .WithMany()
                   .HasForeignKey(o => o.StartedBy)
                   .IsRequired(false);
        }
    }
}
