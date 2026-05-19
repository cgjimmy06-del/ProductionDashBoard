using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public class TimeSlotLookup
    {
        public int TimeSlotId { get; set; } // PK
        public TimeSpan StartAt { get; set; }
        public TimeSpan EndAt { get; set; }
        public string? Label { get; set; }
        public bool IsCrossDay { get; set; }

        public ICollection<InspectionRecord> InspectionRecords { get; set; } = [];
    }
    public class TimeSlotLookupConfiguration : IEntityTypeConfiguration<TimeSlotLookup>
    {
        public void Configure(EntityTypeBuilder<TimeSlotLookup> builder)
        {
            builder.ToTable("timeslot_lookup");

            builder.HasKey(t => t.TimeSlotId);
            builder.Property(t => t.TimeSlotId).HasColumnName("timeslot_id");

            builder.Property(t => t.StartAt).HasColumnName("start_at");
            builder.Property(t => t.EndAt).HasColumnName("end_at");
            builder.Property(t => t.Label).HasColumnName("label");
            builder.Property(t => t.IsCrossDay).HasColumnType("iscrossday").HasDefaultValue(false);
        }
    }
}
