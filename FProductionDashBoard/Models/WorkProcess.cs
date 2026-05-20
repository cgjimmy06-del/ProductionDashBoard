using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

namespace FProductionDashBoard.Models
{
    // 工序 type-lookup：由開發者於 SSMS 維護，UI 唯讀（比照 MaterialType）
    public class WorkProcess
    {
        public int ProcessId { get; set; } // 人為定義 PK，不自動產生
        public string Name { get; set; } = string.Empty;
        public string? ErpCode { get; set; }
        public string? Description { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public ICollection<SopChecklist> SopChecklists { get; set; } = new List<SopChecklist>();
    }

    public class WorkProcessConfiguration : IEntityTypeConfiguration<WorkProcess>
    {
        public void Configure(EntityTypeBuilder<WorkProcess> builder)
        {
            builder.ToTable("work_process");

            builder.HasKey(w => w.ProcessId);
            // 人為定義鍵：不加 ValueGeneratedOnAdd()
            builder.Property(w => w.ProcessId).HasColumnName("process_id").ValueGeneratedNever();

            builder.Property(w => w.Name).HasColumnName("name");
            builder.Property(w => w.ErpCode).HasColumnName("erp_code");
            builder.Property(w => w.Description).HasColumnName("description");
            builder.Property(w => w.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()");
            builder.Property(w => w.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()");
        }
    }
}
