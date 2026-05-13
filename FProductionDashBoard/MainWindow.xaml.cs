using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FProductionDashBoard
{
    public partial class MainWindow : Window
    {
        private double _savedLogPanelHeight = 150;

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Closed += OnWindowClosed;
        }

        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            var toolBar = (ToolBar)sender;
            if (toolBar.Template.FindName("OverflowButton", toolBar) is ToggleButton toolBarButton)
                toolBarButton.SetResourceReference(Control.BackgroundProperty, "PrimaryBackgroundBrush");
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ViewModels.MainViewModel vm)
                vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MainViewModel.IsLogPanelVisible)
                && sender is ViewModels.MainViewModel vm)
                ApplyLogPanelVisibility(vm.IsLogPanelVisible);
        }

        private void ApplyLogPanelVisibility(bool isVisible)
        {
            var splitterRow = ContentGrid.RowDefinitions[1];
            var panelRow    = ContentGrid.RowDefinitions[2];

            if (isVisible)
            {
                splitterRow.Height = new GridLength(5);
                panelRow.Height    = new GridLength(_savedLogPanelHeight);
            }
            else
            {
                if (panelRow.Height.Value > 0)
                    _savedLogPanelHeight = panelRow.Height.Value;
                splitterRow.Height = new GridLength(0);
                panelRow.Height    = new GridLength(0);
            }
        }
    }
}