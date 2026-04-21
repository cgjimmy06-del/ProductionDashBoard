using FProductionDashBoard.Services;

namespace FProductionDashBoard.ViewModels
{
    public class SettingViewModel
    {
        public EquipmentSettingViewModel Equipment { get; }
        public EmployeeSettingViewModel Employee { get; }

        public SettingViewModel(LogService log, IDataService dataService, AuthorizationService auth)
        {
            Equipment = new EquipmentSettingViewModel(log, dataService);
            Employee = new EmployeeSettingViewModel(log, dataService);
        }
    }
}
