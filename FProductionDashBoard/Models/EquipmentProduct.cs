using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace FProductionDashBoard.Models
{
    public class EquipmentProduct
    {
        public int EquipmentProductId { get; set; } // 代理PK
        public int EquipmentId { get; set; } // FK
        public int SeqNo { get; set; }
        public int SopId { get; set; } // FK
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public Equipment? Equipment { get; set; }
        public SopChecklist? Sop { get; set; }
    }

    public class EquipmentProductConfiguration : IEntityTypeConfiguration<EquipmentProduct>
    {
        public void Configure(EntityTypeBuilder<EquipmentProduct> builder)
        {
            builder.ToTable("equipment_product");

            builder.HasKey(e => e.EquipmentProductId);
            builder.Property(e => e.EquipmentProductId).HasColumnName("equipment_product_id").ValueGeneratedOnAdd();

            builder.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            builder.Property(e => e.SeqNo).HasColumnName("seq_no");
            builder.Property(e => e.SopId).HasColumnName("sop_id");
            builder.Property(e => e.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()");

            builder.HasOne(e => e.Equipment)
                   .WithMany()
                   .HasForeignKey(e => e.EquipmentId);

            builder.HasOne(e => e.Sop)
                   .WithMany(s => s.EquipmentProducts)
                   .HasForeignKey(e => e.SopId);

            builder.HasIndex(e => new { e.EquipmentId, e.SeqNo }).IsUnique();
        }
    }
}
