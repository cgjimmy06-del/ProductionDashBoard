using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace FProductionDashBoard.ViewModels
{
    public partial class PanelViewModel : ObservableObject
    {
        private readonly Action<PanelViewModel, NavMode> _switchHandler;
        private bool _isSwitching;

        [ObservableProperty]
        private object? content;

        [ObservableProperty]
        private NavMode currentNavMode = NavMode.Home;

        [ObservableProperty]
        private bool showHeader = false;

        public IEnumerable<NavMode> AvailableModes { get; } = Enum.GetValues<NavMode>();

        public PanelViewModel(Action<PanelViewModel, NavMode> switchHandler)
        {
            _switchHandler = switchHandler;
        }

        partial void OnCurrentNavModeChanged(NavMode value)
        {
            if (_isSwitching) return;
            _switchHandler(this, value);
        }

        internal void SetMode(NavMode mode)
        {
            _isSwitching = true;
            CurrentNavMode = mode;
            _isSwitching = false;
        }
    }
}
