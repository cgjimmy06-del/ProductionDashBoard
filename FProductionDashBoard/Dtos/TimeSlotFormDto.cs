using System;

namespace FProductionDashBoard.Dtos
{
    public class TimeSlotFormDto
    {
        public int TimeSlotId { get; set; }
        public TimeSpan StartAt { get; set; }
        public TimeSpan EndAt { get; set; }
        public bool IsCrossDay { get; set; }
        public string? Label { get; set; }
    }
}
