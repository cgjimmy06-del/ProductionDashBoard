using FProductionDashBoard.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;

namespace FProductionDashBoard.UserControls
{
    public partial class ChartView : UserControl
    {
        public ChartView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // ChartViewModel 為 Scoped 快取重用，每次導覽進入時重載資料
            if (DataContext is ChartViewModel vm && vm.LoadCommand.CanExecute(null))
                vm.LoadCommand.Execute(null);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ChartViewModel oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is ChartViewModel newVm)
            {
                newVm.PropertyChanged += OnVmPropertyChanged;
                RebuildTableColumns(newVm);
            }
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChartViewModel.TableColumns) && sender is ChartViewModel vm)
                RebuildTableColumns(vm);
        }

        // DataGridColumn 非視覺樹成員無法用 XAML 依定義動態綁定，比照 ScheduleView 由 code-behind 維護
        private void RebuildTableColumns(ChartViewModel vm)
        {
            ChartTable.Columns.Clear();
            if (vm.TableColumns.Count == 0) return;

            if (!string.IsNullOrEmpty(vm.IndicatorFieldId))
            {
                var ellipse = new FrameworkElementFactory(typeof(Ellipse));
                ellipse.SetValue(WidthProperty, 10d);
                ellipse.SetValue(HeightProperty, 10d);
                ellipse.SetBinding(Shape.FillProperty, new Binding(nameof(ChartRowViewModel.IndicatorBrushKey))
                {
                    Converter = (IValueConverter)Application.Current.Resources["BrushKeyToBrushConverter"],
                });
                ChartTable.Columns.Add(new DataGridTemplateColumn
                {
                    Width = 40,
                    CanUserResize = false,
                    CellTemplate = new DataTemplate { VisualTree = ellipse },
                });
            }

            foreach (var col in vm.TableColumns)
                ChartTable.Columns.Add(new DataGridTextColumn
                {
                    Header = col.Header,
                    Binding = new Binding($"Display[{col.FieldId}]"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                });
        }
    }
}
