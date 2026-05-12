using System.Collections.Generic;

namespace FProductionDashBoard.ViewModels
{
    public enum NavMode { Home, Operation, View, List, Equipment, Order, SystemSettings }
    public enum LayoutMode { Single, HorizontalSplit, VerticalSplit, Quad }

    public record NavModePolicy(
        bool IsRepeatable,
        bool ClearOnUserChange
    );

    public static class NavModeDescriptor
    {
        private static readonly Dictionary<NavMode, NavModePolicy> _map = new()
        {
            [NavMode.Home]          = new(IsRepeatable: true,  ClearOnUserChange: false),
            [NavMode.Operation]     = new(IsRepeatable: true,  ClearOnUserChange: false),
            [NavMode.View]          = new(IsRepeatable: true,  ClearOnUserChange: false),
            [NavMode.Order]         = new(IsRepeatable: true,  ClearOnUserChange: false),
            [NavMode.List]          = new(IsRepeatable: false, ClearOnUserChange: true),
            [NavMode.Equipment]     = new(IsRepeatable: false, ClearOnUserChange: true),
            [NavMode.SystemSettings]= new(IsRepeatable: false, ClearOnUserChange: true),
        };

        public static NavModePolicy Of(NavMode mode) => _map[mode];
    }
}
