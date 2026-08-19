using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace FProductionDashBoard.Models
{
    public enum LocationType
    {
        Receiving   = 1, // 收料區
        Buffer      = 2, // 緩存區
        MachineSide = 3, // 機邊
        Shipping    = 4  // 出貨區
    }

    public class StorageLocation
    {
        public int LocationId { get; set; } // 代理PK
        public string Code { get; set; } = string.Empty;
        public string? AmrStationCode { get; set; } // 預留（PR1 不使用）
        public string? Zone { get; set; }
        public LocationType LocationType { get; set; }
        public int? Capacity { get; set; }      // 最大箱數，PR1 不啟用擋位
        public double? MapX { get; set; }        // 預留座標（PR1 不使用）
        public double? MapY { get; set; }
        public int? EquipmentId { get; set; }    // 機邊倉位綁設備
        public bool IsEnabled { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public Equipment? Equipment { get; set; }
    }

    public class StorageLocationConfiguration : IEntityTypeConfiguration<StorageLocation>
    {
        public void Configure(EntityTypeBuilder<StorageLocation> builder)
        {
            builder.ToTable("storage_location");

            builder.HasKey(l => l.LocationId);
            builder.Property(l => l.LocationId).HasColumnName("location_id").ValueGeneratedOnAdd();

            builder.Property(l => l.Code).HasColumnName("code");
            builder.Property(l => l.AmrStationCode).HasColumnName("amr_station_code");
            builder.Property(l => l.Zone).HasColumnName("zone");
            builder.Property(l => l.LocationType).HasColumnName("location_type").HasConversion<string>();
            builder.Property(l => l.Capacity).HasColumnName("capacity");
            builder.Property(l => l.MapX).HasColumnName("map_x");
            builder.Property(l => l.MapY).HasColumnName("map_y");
            builder.Property(l => l.EquipmentId).HasColumnName("equipment_id");
            // is_enabled：不宣告 store default，避免非 nullable bool 的 store-default 陷阱（EF 會把 false 誤判為未設定而套 DB 預設）；DB 端 DEFAULT (1) 供原生 insert 使用，服務層一律明確賦值
            builder.Property(l => l.IsEnabled).HasColumnName("is_enabled");
            builder.Property(l => l.CreateAt).HasColumnName("create_at").HasDefaultValueSql("(sysdatetime())");
            builder.Property(l => l.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("(sysdatetime())");

            builder.HasOne(l => l.Equipment)
                   .WithMany()
                   .HasForeignKey(l => l.EquipmentId)
                   .IsRequired(false);

            builder.HasIndex(l => l.Code).IsUnique();
        }
    }
}
