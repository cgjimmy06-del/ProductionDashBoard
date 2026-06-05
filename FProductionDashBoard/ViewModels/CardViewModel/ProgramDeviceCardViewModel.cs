using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Linq;

namespace FProductionDashBoard.ViewModels
{
    public enum ProgramLight { Feasible, Pending, Offset, Error }

    public partial class ProgramDeviceCardViewModel : ObservableObject
    {
        [ObservableProperty] private bool isSelected;
        public int EquipmentId { get; }
        public string DeviceName { get; }
        public int FeasibleCount { get; }
        public int TotalCount { get; }
        public string FeasibleLabel => $"{FeasibleCount}";
        public string TotalLabel => $"/{TotalCount}";
        public ProgramLight Light { get; }

        public ProgramDeviceCardViewModel(int equipmentId, string deviceName,
            int feasibleCount, int totalCount, ProgramLight light)
        {
            EquipmentId = equipmentId;
            DeviceName = deviceName;
            FeasibleCount = feasibleCount;
            TotalCount = totalCount;
            Light = light;
        }

        public static ProgramLight ComputeLight(IReadOnlyCollection<EquipmentProduct> progs)
        {
            if (progs.Any(p => p.ProductionStatus is TuningType.Teaching or TuningType.Infeasible))
                return ProgramLight.Error;
            if (progs.Any(p => p.ProductionStatus == TuningType.Offset))
                return ProgramLight.Offset;
            if (progs.Any(p => p.ProductionStatus == TuningType.Pending))
                return ProgramLight.Pending;
            return ProgramLight.Feasible;
        }
    }
}
