using FProductionDashBoard.Models;
using System;

namespace FProductionDashBoard.Services.Offline.Payloads
{
    public class TuningPayload
    {
        public TuningType TuningType { get; set; }
        public int EquipmentId { get; set; }
        public int EmployeeId { get; set; }
        public int DurationSec { get; set; }
        public int? ProductId { get; set; }
        public DateTime OperatedAt { get; set; }
    }
}
