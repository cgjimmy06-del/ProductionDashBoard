using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

namespace FProductionDashBoard.Models
{
    public enum SopType
    {
        A,
        B,
        C,
        D,
        E,
    }

    // 點檢類型：字串存（SQL 欄位 varchar(10)）
    public enum CheckType
    {
        Station,  // 工位：使用 workstation_no + material_id
        Fixture,  // 治夾具：使用 material_id
        Quantity, // 數量：使用 quantity
        ManHour,  // 工時（秒）：使用 quantity，記錄生產一件所需工時
        Other,    // 其他：使用 content（文字說明型）
    }

    // SOP 點檢明細依 CheckType 篩選 Material 時用的硬編碼 TypeId。
    // 對應 material_type 表中由開發者手動 seed 的兩筆固定列。
    // 若 SSMS 中該表被改動須同步修此檔。
    public static class MaterialTypeIds
    {
        public const int Station = 1; // 工位類物料（CheckType.Station 用）
        public const int Fixture = 2; // 治夾具類物料（CheckType.Fixture 用）
    }

    public class SopChecklist
    {
        public int SopId { get; set; } // 代理PK
        public int ProductId { get; set; } // FK
        public int ProcessId { get; set; } // FK
        public SopType SopType { get; set; } // enum 字串存
        public string? Remark { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public Product? Product { get; set; }
        public WorkProcess? Process { get; set; }
        public ICollection<SopChecklistItem> Items { get; set; } = new List<SopChecklistItem>();
        public ICollection<EquipmentProduct> EquipmentProducts { get; set; } = new List<EquipmentProduct>();
    }

    public class SopChecklistItem
    {
        public int ItemId { get; set; } // 代理PK
        public int SopId { get; set; } // FK
        public int Seq { get; set; }
        public CheckType CheckType { get; set; } // enum 字串存
        public int? WorkstationNo { get; set; } // 工位類型用
        public int? MaterialId { get; set; } // FK，物料/治夾具
        public int? Quantity { get; set; } // 數量類型用
        public string? Content { get; set; } // 文字說明（為未來文字型類型預留）
        public string? Remark { get; set; }

        public SopChecklist? Sop { get; set; }
        public Material? Material { get; set; }
    }

    public class SopChecklistConfiguration : IEntityTypeConfiguration<SopChecklist>
    {
        public void Configure(EntityTypeBuilder<SopChecklist> builder)
        {
            builder.ToTable("sop_checklist");

            builder.HasKey(s => s.SopId);
            builder.Property(s => s.SopId).HasColumnName("sop_id").ValueGeneratedOnAdd();

            builder.Property(s => s.ProductId).HasColumnName("product_id");
            builder.Property(s => s.ProcessId).HasColumnName("process_id");
            builder.Property(s => s.SopType).HasColumnName("sop_type").HasConversion<string>();
            builder.Property(s => s.Remark).HasColumnName("remark");
            builder.Property(s => s.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");
            builder.Property(s => s.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()");

            builder.HasOne(s => s.Product)
                   .WithMany(p => p.SopChecklists)
                   .HasForeignKey(s => s.ProductId);

            builder.HasOne(s => s.Process)
                   .WithMany(w => w.SopChecklists)
                   .HasForeignKey(s => s.ProcessId);

            builder.HasIndex(s => new { s.ProductId, s.ProcessId, s.SopType }).IsUnique();
        }
    }

    public class SopChecklistItemConfiguration : IEntityTypeConfiguration<SopChecklistItem>
    {
        public void Configure(EntityTypeBuilder<SopChecklistItem> builder)
        {
            builder.ToTable("sop_checklist_item");

            builder.HasKey(i => i.ItemId);
            builder.Property(i => i.ItemId).HasColumnName("item_id").ValueGeneratedOnAdd();

            builder.Property(i => i.SopId).HasColumnName("sop_id");
            builder.Property(i => i.Seq).HasColumnName("seq");
            builder.Property(i => i.CheckType).HasColumnName("check_type").HasConversion<string>();
            builder.Property(i => i.WorkstationNo).HasColumnName("workstation_no");
            builder.Property(i => i.MaterialId).HasColumnName("material_id");
            builder.Property(i => i.Quantity).HasColumnName("quantity");
            builder.Property(i => i.Content).HasColumnName("content");
            builder.Property(i => i.Remark).HasColumnName("remark");

            builder.HasOne(i => i.Sop)
                   .WithMany(s => s.Items)
                   .HasForeignKey(i => i.SopId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(i => i.Material)
                   .WithMany()
                   .HasForeignKey(i => i.MaterialId);

            builder.HasIndex(i => new { i.SopId, i.Seq }).IsUnique();
        }
    }
}
