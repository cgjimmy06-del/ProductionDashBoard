using FProductionDashBoard.ViewModels;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;

namespace FProductionDashBoard.UserControls
{
    /// <summary>
    /// 圖表表格的動態欄位建構：DataGridColumn 非視覺樹成員無法用 XAML 依定義動態綁定，
    /// 由 code-behind 維護（比照 ScheduleView）；檢視端 ChartView 與設計器預覽共用。
    /// </summary>
    internal static class ChartTableColumnBuilder
    {
        internal static void Rebuild(DataGrid grid, IReadOnlyList<ChartTableColumn> columns, string? indicatorFieldId)
        {
            grid.Columns.Clear();
            if (columns.Count == 0) return;

            if (!string.IsNullOrEmpty(indicatorFieldId))
            {
                var ellipse = new FrameworkElementFactory(typeof(Ellipse));
                ellipse.SetValue(FrameworkElement.WidthProperty, 10d);
                ellipse.SetValue(FrameworkElement.HeightProperty, 10d);
                ellipse.SetBinding(Shape.FillProperty, new Binding(nameof(ChartRowViewModel.IndicatorBrushKey))
                {
                    Converter = (IValueConverter)Application.Current.Resources["BrushKeyToBrushConverter"],
                });
                grid.Columns.Add(new DataGridTemplateColumn
                {
                    Width = 40,
                    CanUserResize = false,
                    CellTemplate = new DataTemplate { VisualTree = ellipse },
                });
            }

            foreach (var col in columns)
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = col.Header,
                    Binding = new Binding($"Display[{col.FieldId}]"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                });
        }
    }
}
