using FProductionDashBoard.Models;
using System;

namespace FProductionDashBoard.UiModels
{
    /// <summary>右側程式清單與狀態修改 Dialog 顯示用模型（對應 EquipmentProduct，避免 View 直接綁 EF 實體）。</summary>
    public class ProgramItemUiModel
    {
        public int EquipmentProductId { get; set; }
        public int SeqNo { get; set; }
        public string PartNo { get; set; } = string.Empty;
        public string BrandModelProcess { get; set; } = string.Empty;  // "Brand · Model · Process"
        public TuningType ProductionStatus { get; set; }
        public DateTime? UpdateAt { get; set; }

        public static ProgramItemUiModel FromEntity(EquipmentProduct ep)
        {
            var part    = ep.Sop?.Product?.Part;
            var model   = ep.Sop?.Product?.Model;
            var process = ep.Sop?.Process;
            return new()
            {
                EquipmentProductId = ep.EquipmentProductId,
                SeqNo              = ep.SeqNo,
                PartNo             = part?.PartNo ?? "-",
                BrandModelProcess  = $"{part?.Brand} · {model?.Name} · {process?.Name}",
                ProductionStatus   = ep.ProductionStatus,
                UpdateAt           = ep.UpdateAt
            };
        }
    }
}
