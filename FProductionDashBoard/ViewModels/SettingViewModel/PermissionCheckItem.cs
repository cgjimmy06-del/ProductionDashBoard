using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Models;

namespace FProductionDashBoard.ViewModels
{
    public partial class PermissionCheckItem : ObservableObject
    {
        public Permission Permission { get; }
        public bool IsVisible { get; }
        [ObservableProperty] private bool isSelected;

        public PermissionCheckItem(Permission permission, bool isSelected, bool isVisible = true)
        {
            Permission = permission;
            this.isSelected = isSelected;
            IsVisible = isVisible;
        }
    }
}
