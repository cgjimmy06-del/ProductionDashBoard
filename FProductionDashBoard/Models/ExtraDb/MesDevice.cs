using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FProductionDashBoard.Models.Extra
{
    public class MesDevice
    {
        public string DeviceId { get; set; } = string.Empty;
        public string? Group { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string? SerialNum { get; set; } = string.Empty;
        public string? Factory { get; set; } = string.Empty;
        public string? Building { get; set; } = string.Empty;
        public string? Floor { get; set; } = string.Empty;
    }

    public class MesDeviceConfiguration : IEntityTypeConfiguration<MesDevice>
    {
        public void Configure(EntityTypeBuilder<MesDevice> builder)
        {
            builder.HasKey(e => e.DeviceId);
            builder.ToTable("MES_Device");
            builder.Property(e => e.DeviceId).HasColumnName("DeviceID");
            builder.Property(e => e.Group).HasColumnName("Group");
            builder.Property(e => e.Name).HasColumnName("Name");
            builder.Property(e => e.Ip).HasColumnName("IP");
            builder.Property(e => e.SerialNum).HasColumnName("SerialNum");
            builder.Property(e => e.Factory).HasColumnName("Factory");
            builder.Property(e => e.Building).HasColumnName("Building");
            builder.Property(e => e.Floor).HasColumnName("Floor");
        }
    }
}
