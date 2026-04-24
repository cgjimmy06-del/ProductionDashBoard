using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public class MaterialType
    {
        public int TypeId { get; set; } // PK
        public string Name { get; set; } = string.Empty;

        // 對應 Materials
        public ICollection<Material> Materials { get; set; } = new List<Material>();
    }
    public class EquipmentType
    {
        public int TypeId { get; set; } // PK
        public string Name { get; set; } = string.Empty;
        public string? Protocol { get; set; }

        // 對應 Equipments
        public ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
    }
    public class ListType
    {
        public int TypeId { get; set; } // PK
        public string Name { get; set; } = string.Empty;
        public string? Remark { get; set; }

        // 對應 Equipments
        public ICollection<ErrorList> ErrorLists { get; set; } = new List<ErrorList>();
    }

    public class MaterialTypeConfiguration : IEntityTypeConfiguration<MaterialType>
    {
        public void Configure(EntityTypeBuilder<MaterialType> builder)
        {
            builder.ToTable("material_type");

            builder.HasKey(t => t.TypeId);

            builder.Property(t => t.TypeId).HasColumnName("type_id");
            builder.Property(t => t.Name).HasColumnName("name");
        }
    }
    public class EquipmentTypeConfiguration : IEntityTypeConfiguration<EquipmentType>
    {
        public void Configure(EntityTypeBuilder<EquipmentType> builder)
        {
            builder.ToTable("equipment_type");

            builder.HasKey(t => t.TypeId);

            builder.Property(t => t.TypeId).HasColumnName("type_id");
            builder.Property(t => t.Name).HasColumnName("name");
            builder.Property(t => t.Protocol).HasColumnName("protocol");
        }
    }
    public class ListTypeConfiguration : IEntityTypeConfiguration<ListType>
    {
        public void Configure(EntityTypeBuilder<ListType> builder)
        {
            builder.ToTable("list_type");

            builder.HasKey(t => t.TypeId);

            builder.Property(t => t.TypeId).HasColumnName("type_id");
            builder.Property(t => t.Name).HasColumnName("name");
            builder.Property(t => t.Remark).HasColumnName("remark");
        }
    }
}
