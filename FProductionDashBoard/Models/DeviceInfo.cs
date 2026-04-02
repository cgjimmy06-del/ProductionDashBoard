using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static Dapper.SqlMapper;

namespace FProductionDashBoard.Models
{
    public partial class DeviceInfo : ObservableObject
    {
        public required string DeviceID { get; set; } // 主鍵
        public required string Name { get; set; }
        public string IP { get; set; } = "none";
        public int Port { get; set; } = 0;
        public string? Factory { get; set; }
        public string? Building { get; set; }
        public string? Floor { get; set; }

        [ObservableProperty]
        private int status;
        public string? Description { get; set; } // 之後用來mapping做分類查詢 

        //public int WorkerID { get; set; }       // 外鍵
        //public WorkerInfo? Worker { get; set; }      // 導覽屬性
    }

    public class DeviceInfoConfiguration : IEntityTypeConfiguration<DeviceInfo>
    {
        public void Configure(EntityTypeBuilder<DeviceInfo> builder)
        {
            builder.ToTable("equipment");
            builder.HasKey(d => d.DeviceID);
            builder.Property(d => d.DeviceID).HasColumnName("equipment_id");
            builder.Property(d => d.Name).HasColumnName("name");
            builder.Property(d => d.IP).HasColumnName("ip");
            builder.Property(d => d.Port).HasColumnName("port");
            builder.Property(d => d.Factory).HasColumnName("factory");
            builder.Property(d => d.Building).HasColumnName("building");
            builder.Property(d => d.Floor).HasColumnName("floor");

            // 忽略額外屬性
            builder.Ignore(d => d.Description);
            builder.Ignore(d => d.Status);

            // 關聯設定
            //builder.HasOne(d => d.Worker)
            //       .WithMany(w => w.Devices)
            //       .HasForeignKey(d => d.WorkerID);

            // 設定關聯：Worker (1) ↔ Devices (多)
            //modelBuilder.Entity<DeviceInfo>()
            //    .HasOne(d => d.Worker)
            //    .WithMany(w => w.Devices)
            //    .HasForeignKey(d => d.WorkerID);

        }
    }


}
