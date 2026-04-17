using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public class Material
    {
        public int MaterialId { get; set; } // 代理PK
        public string MaterialCode { get; set; } = string.Empty; // UNIQUE
        public string Name { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Specification { get; set; }
        public int? TypeId { get; set; } // FK
        public string? Description { get; set; }
        public int MinimumStock { get; set; } = 0;
        public int QuantityInStock { get; set; } = 0;
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public MaterialType? Type { get; set; } // 導覽屬性
        public ICollection<MaterialReplacementDetail> ReplacementDetails { get; set; } = []; // 對應的明細
    }
    public class MaterialConfiguration : IEntityTypeConfiguration<Material>
    {
        public void Configure(EntityTypeBuilder<Material> builder)
        {
            builder.ToTable("material");

            builder.HasKey(m => m.MaterialId);
            builder.Property(m => m.MaterialId).HasColumnName("material_id").ValueGeneratedOnAdd();

            builder.Property(m => m.MaterialCode).HasColumnName("material_code");
            builder.Property(m => m.Name).HasColumnName("name");
            builder.Property(m => m.Brand).HasColumnName("brand");
            builder.Property(m => m.Specification).HasColumnName("specification");
            builder.Property(m => m.TypeId).HasColumnName("type_id");
            builder.Property(m => m.Description).HasColumnName("description");
            builder.Property(m => m.MinimumStock).HasColumnName("minimum_stock");
            builder.Property(m => m.QuantityInStock).HasColumnName("quantity_instock");
            builder.Property(m => m.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()"); ;
            builder.Property(m => m.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()"); ;

            // 外鍵關聯
            builder.HasOne(m => m.Type)
                   .WithMany(t => t.Materials)
                   .HasForeignKey(m => m.TypeId);
        }
    }

}
