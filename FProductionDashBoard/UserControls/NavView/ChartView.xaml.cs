using FProductionDashBoard.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

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

        // 動態欄位建構抽至 ChartTableColumnBuilder 與設計器預覽共用
        private void RebuildTableColumns(ChartViewModel vm)
            => ChartTableColumnBuilder.Rebuild(ChartTable, vm.TableColumns, vm.IndicatorFieldId);
    }
}
