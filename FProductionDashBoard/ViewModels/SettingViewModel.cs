using FProductionDashBoard.Services;

namespace FProductionDashBoard.ViewModels
{
    public class SettingViewModel
    {
        public EquipmentSettingViewModel Equipment { get; }
        public EmployeeSettingViewModel Employee { get; }
        public MaterialSettingViewModel Material { get; }
        public ErrorListSettingViewModel ErrorList { get; }

        public SettingViewModel(LogService log, IDataService dataService, AuthorizationService auth)
        {
            Equipment = new EquipmentSettingViewModel(log, dataService);
            Employee = new EmployeeSettingViewModel(log, dataService);
            Material = new MaterialSettingViewModel(log, dataService);
            ErrorList = new ErrorListSettingViewModel(log, dataService);
        }
    }
}
