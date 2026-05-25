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
        public SopChecklistSettingViewModel SopChecklist { get; }
        public EquipmentProductSettingViewModel EquipmentProduct { get; }

        public SettingViewModel(DashboardCoreServices core, Services.IDialogService dialog)
        {
            Equipment = new EquipmentSettingViewModel(core, dialog);
            Employee = new EmployeeSettingViewModel(core, dialog);
            Material = new MaterialSettingViewModel(core, dialog);
            ErrorList = new ErrorListSettingViewModel(core, dialog);
            TimeSlot = new TimeSlotSettingViewModel(core, dialog);
            RolePermission = new RolePermissionSettingViewModel(core, dialog);
            SopChecklist = new SopChecklistSettingViewModel(core, dialog);
            EquipmentProduct = new EquipmentProductSettingViewModel(core, dialog);
        }
    }
}
