using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace FProductionDashBoard.ViewModels
{
    public partial class PanelViewModel : ObservableObject
    {
        private readonly Func<PanelViewModel, NavMode, bool> _switchHandler;
        private bool _isSwitching;
        private NavMode _previousNavMode = NavMode.Home;

        [ObservableProperty]
        private object? content;

        [ObservableProperty]
        private NavMode currentNavMode = NavMode.Home;

        [ObservableProperty]
        private bool showHeader = false;

        public IEnumerable<NavMode> AvailableModes { get; } = Enum.GetValues<NavMode>();

        public PanelViewModel(Func<PanelViewModel, NavMode, bool> switchHandler)
        {
            _switchHandler = switchHandler;
        }

        partial void OnCurrentNavModeChanged(NavMode value)
        {
            if (_isSwitching) return;
            bool success = _switchHandler(this, value);
            if (!success)
            {
                var prev = _previousNavMode;
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,() => SetMode(prev)); 
            }
            else _previousNavMode = value;
        }

        internal void SetMode(NavMode mode)
        {
            _isSwitching = true;
            CurrentNavMode = mode;
            _isSwitching = false;
        }
    }
}
