using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Linq;

namespace FProductionDashBoard.ViewModels
{
    public enum ScheduleCardColor { Green, Orange, Blue, Gray }

    public enum ScheduleCardLoadLevel { None, Low, Mid, High }

    public partial class ScheduleEquipmentCardViewModel : ObservableObject
    {
        public int EquipmentId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public List<EquipmentProduct> AllEquipmentProducts { get; init; } = new();

        // Layer 1
        public bool HasAnyFeasibleEp
            => AllEquipmentProducts.Any(ep => ep.ProductionStatus == TuningType.Feasible);

        public bool HasAnyFeasibleEpForKeys(IEnumerable<(int ProductId, int ProcessId)> keys)
            => AllEquipmentProducts.Any(ep =>
                ep.ProductionStatus == TuningType.Feasible &&
                keys.Any(k => k.ProductId == ep.Sop?.ProductId && k.ProcessId == ep.Sop?.ProcessId));

        public bool HasAnyCompatibleEpForKeys(IEnumerable<(int ProductId, int ProcessId)> keys)
            => AllEquipmentProducts.Any(ep =>
                keys.Any(k => k.ProductId == ep.Sop?.ProductId && k.ProcessId == ep.Sop?.ProcessId));

        // 焦點模式選取狀態
        [ObservableProperty] private bool isSelected;

        // Layer 2（依選取排單動態更新）
        [ObservableProperty] private bool isCompatibleWithSelectedSchedule = true;
        public bool HasMatchingProgram { get; set; } = true;
        public List<EquipmentProduct> CompatibleEquipmentProducts { get; set; } = new();

        public int FeasibleMatchingCount { get; set; }
        public int TotalMatchingCount { get; set; }

        // 接單統計
        [ObservableProperty] private int pendingOrderCount;
        [ObservableProperty] private int inProductionOrderCount;
        [ObservableProperty] private int totalPendingQty;

        public int ActiveOrderCount => PendingOrderCount + InProductionOrderCount;

        partial void OnPendingOrderCountChanged(int value)      => OnPropertyChanged(nameof(ActiveOrderCount));
        partial void OnInProductionOrderCountChanged(int value) => OnPropertyChanged(nameof(ActiveOrderCount));

        [ObservableProperty] private string? inProductionInfo;
        [ObservableProperty] private ProgramTuningRecord? activeTuning;

        public ScheduleCardColor CardColor =>
            !HasAnyFeasibleEp                 ? ScheduleCardColor.Gray :
            !IsCompatibleWithSelectedSchedule ? ScheduleCardColor.Gray :
            InProductionOrderCount > 0        ? ScheduleCardColor.Blue :
            PendingOrderCount > 0             ? ScheduleCardColor.Orange :
                                                ScheduleCardColor.Green;

        public int TotalProgramCount => AllEquipmentProducts.Count;

        public int SortOrder => CardColor switch
        {
            ScheduleCardColor.Green  => 0,
            ScheduleCardColor.Orange => 1,
            ScheduleCardColor.Blue   => 2,
            ScheduleCardColor.Gray   => 3,
            _                        => 4
        };

        public ScheduleCardLoadLevel LoadLevel =>
            TotalPendingQty == 0  ? ScheduleCardLoadLevel.None :
            TotalPendingQty < 100 ? ScheduleCardLoadLevel.Low  :
            TotalPendingQty <= 500? ScheduleCardLoadLevel.Mid  :
                                    ScheduleCardLoadLevel.High;

        partial void OnIsCompatibleWithSelectedScheduleChanged(bool value)
        {
            OnPropertyChanged(nameof(CardColor));
            OnPropertyChanged(nameof(SortOrder));
        }

        public void ResetForLayer1()
        {
            IsCompatibleWithSelectedSchedule = true;
            HasMatchingProgram = true;
            CompatibleEquipmentProducts = new();
            FeasibleMatchingCount = 0;
            TotalMatchingCount = 0;
        }
    }
}
