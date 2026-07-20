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
            _licenseService.LicenseUpdated += OnLicenseUpdated;
        }

        private void OnLicenseUpdated(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsChartLocked));
            OnPropertyChanged(nameof(IsScheduleLocked));
            OnPropertyChanged(nameof(IsProgramLibLocked));
            OnPropertyChanged(nameof(IsProductInOutLocked));
            OnPropertyChanged(nameof(IsAiAgentLicensed));
            if (!IsAiAgentLicensed && IsAiAgentVisible)
                IsAiAgentVisible = false;
        }

        private static LicensedFeature? GetRequiredFeature(NavMode mode) => mode switch
        {
            // 需授權
            NavMode.Chart          => LicensedFeature.Charts,
            NavMode.Schedule       => LicensedFeature.Scheduling,
            NavMode.ProgramLibrary => LicensedFeature.ProgramLibrary,
            NavMode.ProductInOut   => LicensedFeature.MaterialManagement,
            // 永久免費
            NavMode.Home           => null,
            NavMode.Operation      => null,
            NavMode.List           => null,
            NavMode.Equipment      => null,
            NavMode.SystemSettings => null,
            // 未分類 → 立即報錯，強制開發者主動分類
            _ => throw new InvalidOperationException($"NavMode {mode} 尚未分類授權需求")
        };

        public bool IsChartLocked        => !_licenseService.IsFeatureEnabled(LicensedFeature.Charts);
        public bool IsScheduleLocked     => !_licenseService.IsFeatureEnabled(LicensedFeature.Scheduling);
        public bool IsProgramLibLocked   => !_licenseService.IsFeatureEnabled(LicensedFeature.ProgramLibrary);
        public bool IsProductInOutLocked => !_licenseService.IsFeatureEnabled(LicensedFeature.MaterialManagement);
        public bool IsAiAgentLicensed    => _licenseService.IsFeatureEnabled(LicensedFeature.AiAgent);

        public void SwitchMode(NavMode mode)
        {
            if (mode.Equals(CurrentNavMode)) return;
            bool success = SwitchPanelContent(Panel1, mode);
            if (success) CurrentNavMode = mode;
        }

        private bool SwitchPanelContent(PanelViewModel panel, NavMode mode)
        {
            LicensedFeature? requiredFeature;
            try { requiredFeature = GetRequiredFeature(mode); }
            catch (InvalidOperationException)
            {
                _core.Log.AddErrorLog($"[SwitchPanelContent] NavMode {mode} 未設定授權映射，拒絕存取");
                return false;
            }
            if (requiredFeature.HasValue && !_licenseService.IsFeatureEnabled(requiredFeature.Value))
            {
                _core.Log.AddLog("[SwitchPanelContent] 此功能需要有效授權", LogLevel.Error);
                return false;
            }

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
                    if (!_core.Authorization.HasPermission(PermissionId.View)) return false;
                    content = _serviceProvider.GetRequiredService<HardwareViewModel>();
                    break;

                case NavMode.Chart:
                    if (!_core.Authorization.HasPermission(PermissionId.View)) return false;
                    content = _serviceProvider.GetRequiredService<ChartViewModel>();
                    break;

                case NavMode.Schedule:
                    if (!_core.Authorization.HasAnyPermission(PermissionId.Order, PermissionId.Schedule)) return false;
                    content = _serviceProvider.GetRequiredService<ScheduleViewModel>();
                    break;

                case NavMode.ProductInOut:
                    if (!_core.Authorization.HasAnyPermission(PermissionId.Order, PermissionId.Schedule)) return false;
                    content = _serviceProvider.GetRequiredService<ProductInOutViewModel>();
                    break;

                case NavMode.ProgramLibrary:
                    if (!_core.Authorization.HasPermission(PermissionId.Schedule)) return false;
                    content = _serviceProvider.GetRequiredService<ProgramLibraryViewModel>();
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

        /// <summary>釋放所有設備容器（連帶釋放每張 DeviceCard 的重連 loop 與 ABB/Modbus 連線）。由 Dispose 呼叫。</summary>
        private void DisposePanelContainers()
        {
            _deviceContainer?.Dispose();
            _deviceContainer = null;
            foreach (var vm in _panelContainers.Values) vm.Dispose();
            _panelContainers.Clear();
        }
    }
}
