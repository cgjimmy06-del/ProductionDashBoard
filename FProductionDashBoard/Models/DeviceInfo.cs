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

namespace FProductionDashBoard
{
    public partial class DeviceInfo : ObservableObject
    {
        [ObservableProperty]
        private bool lightOn;

        [ObservableProperty]
        private int status;

        public required string DeviceID { get; set; } // 主鍵
        public required string Name { get; set; }
        public required string IP { get; set; }
        public string Description { get; set; } = "";

        //public int WorkerID { get; set; }       // 外鍵
        //public WorkerInfo? Worker { get; set; }      // 導覽屬性
    }

    public class DeviceInfoConfiguration : IEntityTypeConfiguration<DeviceInfo>
    {
        public void Configure(EntityTypeBuilder<DeviceInfo> builder)
        {
            builder.ToTable("MES_Device");
            builder.HasKey(d => d.DeviceID);
            builder.Property(d => d.DeviceID).HasColumnName("DeviceID");
            builder.Property(d => d.Name).HasColumnName("Name");
            builder.Property(d => d.IP).HasColumnName("IP");

            // 忽略額外屬性
            builder.Ignore(d => d.Description);
            builder.Ignore(d => d.LightOn);
            builder.Ignore(d => d.Status);

            // 關聯設定
            //builder.HasOne(d => d.Worker)
            //       .WithMany(w => w.Devices)
            //       .HasForeignKey(d => d.WorkerID);
        }
    }


}
