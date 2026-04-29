using FProductionDashBoard.Services;

namespace FProductionDashBoard.ViewModels
{
    public class SettingViewModel
    {
        public EquipmentSettingViewModel Equipment { get; }
        public EmployeeSettingViewModel Employee { get; }
        public MaterialSettingViewModel Material { get; }
        public ErrorListSettingViewModel ErrorList { get; }
        public TimeSlotSettingViewModel TimeSlot { get; }
        public RolePermissionSettingViewModel RolePermission { get; }

        public SettingViewModel(DashboardCoreServices core)
        {
            Equipment = new EquipmentSettingViewModel(core);
            Employee = new EmployeeSettingViewModel(core);
            Material = new MaterialSettingViewModel(core);
            ErrorList = new ErrorListSettingViewModel(core);
            TimeSlot = new TimeSlotSettingViewModel(core);
            RolePermission = new RolePermissionSettingViewModel(core);
        }
    }
}
