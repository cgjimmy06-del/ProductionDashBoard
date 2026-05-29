using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class MainViewModel
    {
        public ObservableCollection<object> Cards { get; set; } = new();
        [ObservableProperty] public object? mainCard;
        [ObservableProperty] private LayoutMode currentLayout = LayoutMode.Single;
        [ObservableProperty] private NavMode currentNavMode = NavMode.Home;

        private OperationViewModel? _deviceContainer;
        private readonly Dictionary<PanelViewModel, OperationViewModel> _panelContainers = new();

        public PanelViewModel Panel1 { get; private set; } = null!;
        public PanelViewModel Panel2 { get; private set; } = null!;
        public PanelViewModel Panel3 { get; private set; } = null!;
        public PanelViewModel Panel4 { get; private set; } = null!;
        private PanelViewModel[] _panels = [];

        public IRelayCommand<LayoutMode> SetLayoutCommand { get; private set; } = null!;
        public IRelayCommand SwitchModeCommand { get; private set; } = null!;

        partial void InitializePanelLayout()
        {
            SwitchModeCommand = new RelayCommand<NavMode>(SwitchMode,
                (_) => _core.Authorization.HasPermission(PermissionId.View));
            Panel1 = new PanelViewModel(SwitchPanelContent);
            Panel2 = new PanelViewModel(SwitchPanelContent);
            Panel3 = new PanelViewModel(SwitchPanelContent);
            Panel4 = new PanelViewModel(SwitchPanelContent);
            _panels = [Panel1, Panel2, Panel3, Panel4];
            SetLayoutCommand = new RelayCommand<LayoutMode>(SetLayout);
            foreach (var ipanel in _panels)
                SwitchPanelContent(ipanel, NavMode.Home);
        }

        public void SwitchMode(NavMode mode)
        {
            if (mode.Equals(CurrentNavMode)) return;
            bool success = SwitchPanelContent(Panel1, mode);
            if (success) CurrentNavMode = mode;
        }

        private bool SwitchPanelContent(PanelViewModel panel, NavMode mode)
        {
            object? content = null;
            switch (mode)
            {
                case NavMode.Home:
                    content = _serviceProvider.GetRequiredService<HomeViewModel>();
                    break;

                case NavMode.Operation:
                    if (!_core.Authorization.HasAnyPermission(
                        PermissionId.OperateInspection, PermissionId.OperateMaterial,
                        PermissionId.OperateTuning, PermissionId.Order)) return false;
                    content = GetOrCreateContainer(panel);
                    break;

                case NavMode.List:
                    if (!_core.Authorization.HasPermission(PermissionId.Edit)) return false;
                    content = _serviceProvider.GetRequiredService<SettingViewModel>();
                    break;

                case NavMode.Equipment:
                    if (!_core.Authorization.HasPermission(PermissionId.Setting)) return false;
                    content = _serviceProvider.GetRequiredService<HardwareViewModel>();
                    break;

                case NavMode.Order:
                    if (!_core.Authorization.HasAnyPermission(PermissionId.Order, PermissionId.Schedule)) return false;
                    break;

                case NavMode.SystemSettings:
                    if (!_core.Authorization.HasPermission(PermissionId.Setting)) return false;
                    var ssVm = _serviceProvider.GetRequiredService<SystemSettingsViewModel>();
                    ssVm.LoadFromSettings();
                    content = ssVm;
                    break;
            }

            var policy = NavModeDescriptor.Of(mode);
            if (!policy.IsRepeatable)
            {
                var occupied = _panels.FirstOrDefault(p => p != panel && p.CurrentNavMode == mode);
                if (occupied != null)
                    SwitchPanelContent(occupied, NavMode.Home);
            }

            panel.SetMode(mode);
            panel.Content = content;
            if (panel == Panel1) MainCard = content;
            return true;
        }

        private OperationViewModel GetOrCreateContainer(PanelViewModel panel)
        {
            if (panel == Panel1)
            {
                _deviceContainer ??= _serviceProvider.GetRequiredService<OperationViewModel>();
                return _deviceContainer;
            }
            if (!_panelContainers.TryGetValue(panel, out var vm))
                _panelContainers[panel] = vm = _serviceProvider.GetRequiredService<OperationViewModel>();
            return vm;
        }

        private void SetLayout(LayoutMode layout)
        {
            CurrentLayout = layout;
            bool isMulti = layout != LayoutMode.Single;
            foreach (var p in _panels) p.ShowHeader = isMulti;
        }
    }
}
