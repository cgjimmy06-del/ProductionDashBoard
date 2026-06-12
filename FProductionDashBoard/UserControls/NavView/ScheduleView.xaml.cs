using FProductionDashBoard.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FProductionDashBoard.UserControls
{
    public partial class ScheduleView : UserControl
    {
        public ScheduleView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private ScheduleViewModel? _vm;

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = e.NewValue as ScheduleViewModel;
            if (_vm != null) _vm.PropertyChanged += OnVmPropertyChanged;
            UpdateFocusModeColumns();
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ScheduleViewModel.IsCardWallVisible)
                                or nameof(ScheduleViewModel.IsEquipmentDetailVisible)
                                or nameof(ScheduleViewModel.SelectedEquipmentCard))
                UpdateFocusModeColumns();
        }

        private void UpdateFocusModeColumns()
        {
            var inFocus = _vm?.IsEquipmentDetailVisible == true;
            DerivedBadgeColumn.Visibility = inFocus ? Visibility.Collapsed : Visibility.Visible;
            FocusAssignColumn.Visibility  = inFocus ? Visibility.Visible   : Visibility.Collapsed;
            FocusAssignHeaderMachineName.Text = _vm?.SelectedEquipmentCard?.Name ?? string.Empty;
        }
    }
}
