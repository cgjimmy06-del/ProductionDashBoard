using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

namespace FProductionDashBoard.Models
{
    public class Product
    {
        public int ProductId { get; set; } // 代理PK
        public int PartId { get; set; } // FK
        public int ModelId { get; set; } // FK
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public ProductPart? Part { get; set; }
        public ProductModel? Model { get; set; }
        public ICollection<SopChecklist> SopChecklists { get; set; } = new List<SopChecklist>();
    }

    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("product");

            builder.HasKey(p => p.ProductId);
            builder.Property(p => p.ProductId).HasColumnName("product_id").ValueGeneratedOnAdd();

            builder.Property(p => p.PartId).HasColumnName("part_id");
            builder.Property(p => p.ModelId).HasColumnName("model_id");
            builder.Property(p => p.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");
            builder.Property(p => p.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()");

            builder.HasOne(p => p.Part)
                   .WithMany(pp => pp.Products)
                   .HasForeignKey(p => p.PartId);

            builder.HasOne(p => p.Model)
                   .WithMany(m => m.Products)
                   .HasForeignKey(p => p.ModelId);

            builder.HasIndex(p => new { p.PartId, p.ModelId }).IsUnique();
        }
    }
}
