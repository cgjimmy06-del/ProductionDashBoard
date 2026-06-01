using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

namespace FProductionDashBoard.Models
{
    public class ProductPart
    {
        public int PartId { get; set; } // 代理PK
        public string PartNo { get; set; } = string.Empty; // UNIQUE, varchar(8)
        public string? Brand { get; set; }
        public string? Name { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public class ProductPartConfiguration : IEntityTypeConfiguration<ProductPart>
    {
        public void Configure(EntityTypeBuilder<ProductPart> builder)
        {
            builder.ToTable("product_part");

            builder.HasKey(p => p.PartId);
            builder.Property(p => p.PartId).HasColumnName("part_id").ValueGeneratedOnAdd();

            builder.Property(p => p.PartNo).HasColumnName("part_no");
            builder.Property(p => p.Brand).HasColumnName("brand");
            builder.Property(p => p.Name).HasColumnName("name");
            builder.Property(p => p.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");
            builder.Property(p => p.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()");

            builder.HasIndex(p => p.PartNo).IsUnique();
        }
    }
}
