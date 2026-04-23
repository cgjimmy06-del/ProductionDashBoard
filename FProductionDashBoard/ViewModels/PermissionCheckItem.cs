using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Models;

namespace FProductionDashBoard.ViewModels
{
    public partial class PermissionCheckItem : ObservableObject
    {
        public Permission Permission { get; }
        [ObservableProperty] private bool isSelected;

        public PermissionCheckItem(Permission permission, bool isSelected)
        {
            Permission = permission;
            this.isSelected = isSelected;
        }
    }
}
