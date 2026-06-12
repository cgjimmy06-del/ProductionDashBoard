using FProductionDashBoard.Models;
using System;

namespace FProductionDashBoard.UiModels
{
    public class ScheduleUiModel
    {
        public int ScheduleId { get; set; }
        public int ProductId { get; set; }
        public int ProcessId { get; set; }
        public string PartNo { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int? ActualQuantity { get; set; }
        public string? LotNo { get; set; }
        public ScheduleStatus Status { get; set; }

        public int ReceivedBy { get; set; }
        public string ReceivedByName { get; set; } = string.Empty;
        public DateTime? ReceivedAt { get; set; }

        public int? ScheduledBy { get; set; }
        public string? ScheduledByName { get; set; }
        public DateTime? ScheduledAt { get; set; }

        public int? VerifiedBy { get; set; }
        public string? VerifiedByName { get; set; }
        public DateTime? VerifiedAt { get; set; }

        public int? ReleasedBy { get; set; }
        public string? ReleasedByName { get; set; }
        public DateTime? ReleasedAt { get; set; }

        public string? Description { get; set; }
        public int? ParentId { get; set; }

        // UI 衍生欄位，由 ScheduleViewModel 在載入後計算並設定
        public string? DerivedBadge { get; set; }
        public string? DerivedBadgeKey { get; set; }  // stable key for DataTrigger/logic: InProduction / Complete / Partial / CancelNotice
        public string? WaitingDaysText { get; set; }
        public int ActiveEquipmentCount { get; set; }
        public int ActiveOrderCount { get; set; }
        public bool HasActiveOrders => ActiveOrderCount > 0;

        public static ScheduleUiModel FromEntity(Schedule s)
        {
            var part    = s.Product?.Part;
            var model   = s.Product?.Model;
            var process = s.Process;
            return new()
            {
                ScheduleId      = s.ScheduleId,
                ProductId       = s.ProductId,
                ProcessId       = s.ProcessId,
                PartNo          = part?.PartNo ?? "-",
                BrandName       = part?.Brand ?? "-",
                ModelName       = model?.Name ?? "-",
                ProcessName     = process?.Name ?? "-",
                Quantity        = s.Quantity,
                ActualQuantity  = s.ActualQuantity,
                LotNo           = s.LotNo,
                Status          = s.Status,
                ReceivedBy      = s.ReceivedBy,
                ReceivedByName  = s.ReceivedByEmployee?.Name ?? "-",
                ReceivedAt      = s.ReceivedAt,
                ScheduledBy     = s.ScheduledBy,
                ScheduledByName = s.ScheduledByEmployee?.Name,
                ScheduledAt     = s.ScheduledAt,
                VerifiedBy      = s.VerifiedBy,
                VerifiedByName  = s.VerifiedByEmployee?.Name,
                VerifiedAt      = s.VerifiedAt,
                ReleasedBy      = s.ReleasedBy,
                ReleasedByName  = s.ReleasedByEmployee?.Name,
                ReleasedAt      = s.ReleasedAt,
                Description     = s.Description,
                ParentId        = s.ParentId
            };
        }
    }
}
