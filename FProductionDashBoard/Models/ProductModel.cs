using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

namespace FProductionDashBoard.Models
{
    // 型號 type-lookup：由開發者於 SSMS 維護，UI 唯讀（比照 MaterialType）
    public class ProductModel
    {
        public int ModelId { get; set; } // 人為定義 PK，不自動產生
        public string Name { get; set; } = string.Empty;
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public class ProductModelConfiguration : IEntityTypeConfiguration<ProductModel>
    {
        public void Configure(EntityTypeBuilder<ProductModel> builder)
        {
            builder.ToTable("product_model");

            builder.HasKey(m => m.ModelId);
            // 人為定義鍵：不加 ValueGeneratedOnAdd()
            builder.Property(m => m.ModelId).HasColumnName("model_id").ValueGeneratedNever();

            builder.Property(m => m.Name).HasColumnName("name");
            builder.Property(m => m.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");
            builder.Property(m => m.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()");
        }
    }
}
