using FProductionDashBoard.Models;

namespace FProductionDashBoard.UiModels;

public class ListsFromSql
{
    public List<DeviceInfo> DevicesList = new();
    public List<MaterialInfo> MaterialsList = new();
    public List<ErrorInfo> ErrorsList = new();
    public List<UserInfo> UsersList = new();
    public List<TimeSlotLookup> TimeSlotsList = new();
    public List<Role> RolesList = new();
    public List<EquipmentProduct> EquipmentProductsList = new();
}
