using FProductionDashBoard.Models;

namespace FProductionDashBoard.UiModels;

public class ListsFromSql
{
    public List<DeviceInfo> DevicesList { get; set; } = new();
    public List<MaterialInfo> MaterialsList { get; set; } = new();
    public List<ErrorInfo> ErrorsList { get; set; } = new();
    public List<UserInfo> UsersList { get; set; } = new();
    public List<TimeSlotLookup> TimeSlotsList { get; set; } = new();
    public List<Role> RolesList { get; set; } = new();
}
