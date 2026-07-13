using FProductionDashBoard.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FProductionDashBoard.UserControls
{
    public partial class ChartDesignerView : UserControl
    {
        public ChartDesignerView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ChartDesignerViewModel oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is ChartDesignerViewModel newVm)
            {
                newVm.PropertyChanged += OnVmPropertyChanged;
                RebuildTableColumns(newVm);
            }
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChartDesignerViewModel.PreviewTableColumns)
                && sender is ChartDesignerViewModel vm)
                RebuildTableColumns(vm);
        }

        // 大框架預覽與單列樣本共用同一組欄位定義
        private void RebuildTableColumns(ChartDesignerViewModel vm)
        {
            ChartTableColumnBuilder.Rebuild(PreviewTable, vm.PreviewTableColumns, vm.PreviewIndicatorFieldId);
            ChartTableColumnBuilder.Rebuild(SampleTable, vm.PreviewTableColumns, vm.PreviewIndicatorFieldId);
        }
    }
}
