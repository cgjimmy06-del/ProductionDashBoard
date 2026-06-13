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
        private double _savedAiAgentWidth = 350;

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
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.Dispose();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MainViewModel.IsLogPanelVisible)
                && sender is ViewModels.MainViewModel vm)
                ApplyLogPanelVisibility(vm.IsLogPanelVisible);

            if (e.PropertyName == nameof(ViewModels.MainViewModel.IsAiAgentVisible)
                && sender is ViewModels.MainViewModel vm2)
                ApplyAiAgentVisibility(vm2.IsAiAgentVisible);
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

        private void ApplyAiAgentVisibility(bool isVisible)
        {
            var splitterCol = MainAreaGrid.ColumnDefinitions[2];
            var panelCol    = MainAreaGrid.ColumnDefinitions[3];

            if (isVisible)
            {
                splitterCol.Width = new GridLength(5);
                panelCol.Width    = new GridLength(_savedAiAgentWidth);
            }
            else
            {
                if (panelCol.Width.Value > 0)
                    _savedAiAgentWidth = panelCol.Width.Value;
                splitterCol.Width = new GridLength(0);
                panelCol.Width    = new GridLength(0);
            }
        }
    }
}