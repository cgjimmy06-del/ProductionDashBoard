using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

namespace FProductionDashBoard.Models
{
    // 型號：使用者可於 SOP 設定頁新增（代理鍵 IDENTITY，比照 ProductPart）
    public class ProductModel
    {
        public int ModelId { get; set; } // IDENTITY PK，DB 自動產生
        public string Name { get; set; } = string.Empty;
        public string? Remark { get; set; }
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
            builder.Property(m => m.ModelId).HasColumnName("model_id").ValueGeneratedOnAdd();

            builder.Property(m => m.Name).HasColumnName("name");
            builder.Property(m => m.Remark).HasColumnName("remark").HasMaxLength(20);
            builder.HasIndex(m => m.Name).IsUnique().HasDatabaseName("UQ_product_model_name");
            builder.Property(m => m.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");
            builder.Property(m => m.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()");
        }
    }
}
