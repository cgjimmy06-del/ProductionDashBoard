using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FProductionDashBoard.UserControls
{
    public partial class ProgramLibraryView : UserControl
    {
        public ProgramLibraryView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            if (e.NewValue is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += OnViewModelPropertyChanged;
            if (e.NewValue is ViewModels.ProgramLibraryViewModel vm)
                ApplyPanelVisibility(vm.IsDetailVisible);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.ProgramLibraryViewModel.IsDetailVisible)
                && sender is ViewModels.ProgramLibraryViewModel vm)
                ApplyPanelVisibility(vm.IsDetailVisible);
        }

        private void ApplyPanelVisibility(bool isVisible)
        {
            var col0 = ContentGrid.ColumnDefinitions[0];
            var col1 = ContentGrid.ColumnDefinitions[1];
            var col2 = ContentGrid.ColumnDefinitions[2];

            if (isVisible)
            {
                col0.Width = new GridLength(1, GridUnitType.Star);
                col1.Width = new GridLength(5);
                col2.Width = new GridLength(2, GridUnitType.Star);
            }
            else
            {
                col0.Width = new GridLength(1, GridUnitType.Star);
                col1.Width = new GridLength(0);
                col2.Width = new GridLength(0);
            }
        }
    }
}
