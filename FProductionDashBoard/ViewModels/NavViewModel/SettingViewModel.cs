using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Services;

namespace FProductionDashBoard.ViewModels
{
    public partial class SettingViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;

        public EquipmentSettingViewModel Equipment { get; }
        public EmployeeSettingViewModel Employee { get; }
        public MaterialSettingViewModel Material { get; }
        public ErrorListSettingViewModel ErrorList { get; }
        public TimeSlotSettingViewModel TimeSlot { get; }
        public RolePermissionSettingViewModel RolePermission { get; }
        public SopChecklistSettingViewModel SopChecklist { get; }
        public EquipmentProductSettingViewModel EquipmentProduct { get; }

        [ObservableProperty] private bool isGeneralTabVisible;
        [ObservableProperty] private bool isAdminTabVisible;

        public SettingViewModel(DashboardCoreServices core, Services.IDialogService dialog)
        {
            _core = core;
            Equipment = new EquipmentSettingViewModel(core, dialog);
            Employee = new EmployeeSettingViewModel(core, dialog);
            Material = new MaterialSettingViewModel(core, dialog);
            ErrorList = new ErrorListSettingViewModel(core, dialog);
            TimeSlot = new TimeSlotSettingViewModel(core, dialog);
            RolePermission = new RolePermissionSettingViewModel(core, dialog);
            SopChecklist = new SopChecklistSettingViewModel(core, dialog);
            EquipmentProduct = new EquipmentProductSettingViewModel(core, dialog);

            // 依賴單例生命週期（scoped + 單一 root scope，訂閱僅一次）；
            // 若日後改 transient/多 scope，需讓本 VM 實作 IDisposable 解除此訂閱
            _core.Authorization.UserChanged += RefreshTabVisibility;
            RefreshTabVisibility();
        }

        private void RefreshTabVisibility()
        {
            IsGeneralTabVisible = _core.Authorization.HasPermission(PermissionId.Edit);
            IsAdminTabVisible = _core.Authorization.HasPermission(PermissionId.Setting);
        }
    }
}
