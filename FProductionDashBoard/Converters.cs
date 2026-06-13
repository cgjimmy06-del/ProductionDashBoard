using FProductionDashBoard.Models;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FProductionDashBoard
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOn = (bool)value;
            
            if (parameter?.ToString() == "NoError")
            { return isOn ? Application.Current.Resources["SuccessBrush"] : Application.Current.Resources["ErrorBrush"]; }

            if (parameter?.ToString() == "Invert")
                isOn = !isOn;

            return isOn ? Application.Current.Resources["SuccessBrush"] : Application.Current.Resources["ErrorBrush"];
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotImplementedException(); }
    }
    public class BoolToTransparentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOn = (bool)value;

            if (parameter?.ToString() == "NoError")
            { return isOn ? Application.Current.Resources["SuccessBrush"] : Application.Current.Resources["Transparent"]; }

            if (parameter?.ToString() == "Invert")
                isOn = !isOn;

            return isOn ? Application.Current.Resources["SuccessBrush"] : Application.Current.Resources["Transparent"];
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotImplementedException(); }
    }
    public class BoolToAlertIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? PackIconKind.AlertPlusOutline : PackIconKind.AlertOutline;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
    public class IntToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int status = (int)value;

            // 從 App.xaml 資源取顏色
            var resources = Application.Current.Resources;
            return status switch
            {
                0 => (Brush)resources["SuccessBrush"],
                1 => (Brush)resources["WarningBrush"],
                2 => (Brush)resources["ErrorBrush"],
                _ => (Brush)resources["IdleBrush"]
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotImplementedException(); }
    }
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            return string.IsNullOrEmpty(str) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotImplementedException(); }
    }
    public class StringHasValueToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotImplementedException(); }
    }
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                if (parameter?.ToString() == "Invert")
                    b = !b;
                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is Visibility v) && v == Visibility.Visible;
        }
    }
    public class CollapseWidthConverter : IValueConverter
    {
        public double CollapsedWidth { get; set; } = 40;
        public double ExpandedWidth { get; set; } = 200;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isCollapsed = (bool)value;
            if (parameter is string param)
            {
                var parts = param.Split(',');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], out double collapsed) &&
                    double.TryParse(parts[1], out double expanded))
                {
                    return isCollapsed ? collapsed : expanded;
                }
            }
            return isCollapsed ? CollapsedWidth : ExpandedWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotImplementedException(); }
    }
    public class TimeSpanToHhmmConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is TimeSpan ts ? ts.ToString(@"hh\:mm") : "";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class TranslationZhTwConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not IEnumerable<ErrorTranslation> translations) return null;
            return translations.FirstOrDefault(t => t.LanguageCode == "zh-TW")?.Message;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
    public class EqualToConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value?.ToString() == parameter?.ToString();
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
    public class StringEqualityMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values.Length == 2 && values[0]?.ToString() == values[1]?.ToString();
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && value.Equals(Enum.Parse(value.GetType(), parameter.ToString() ?? ""));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Enum.Parse(targetType, parameter.ToString() ?? "") : Binding.DoNothing;
        }
    }
    public class ProgramLightToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resources = Application.Current.Resources;
            return value is ViewModels.ProgramLight light ? light switch
            {
                ViewModels.ProgramLight.Feasible => (Brush)resources["ProgramFeasibleBrush"],
                ViewModels.ProgramLight.Pending  => (Brush)resources["ProgramPendingBrush"],
                ViewModels.ProgramLight.Offset   => (Brush)resources["ProgramOffsetBrush"],
                ViewModels.ProgramLight.Error    => (Brush)resources["ProgramErrorBrush"],
                _                                => (Brush)resources["IdleBrush"],
            } : (Brush)resources["IdleBrush"];
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class TuningTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resources = Application.Current.Resources;
            return value is Models.TuningType t ? t switch
            {
                Models.TuningType.Feasible                         => (Brush)resources["ProgramFeasibleBrush"],
                Models.TuningType.Pending                          => (Brush)resources["ProgramPendingBrush"],
                Models.TuningType.Offset                           => (Brush)resources["ProgramOffsetBrush"],
                Models.TuningType.Teaching or Models.TuningType.Infeasible => (Brush)resources["ProgramErrorBrush"],
                _                                                  => (Brush)resources["IdleBrush"],
            } : (Brush)resources["IdleBrush"];
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class TuningTypeToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resources = Application.Current.Resources;
            var key = value is Models.TuningType t ? t switch
            {
                Models.TuningType.Feasible   => "PlStatFeasible",
                Models.TuningType.Teaching   => "PlStatTeaching",
                Models.TuningType.Offset     => "PlStatOffset",
                Models.TuningType.Pending    => "PlStatPending",
                Models.TuningType.Infeasible => "PlStatInfeasible",
                _                            => null,
            } : null;
            return key != null && resources[key] is string label ? label : value?.ToString() ?? string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class ScheduleStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resources = Application.Current.Resources;
            return value is Models.ScheduleStatus s ? s switch
            {
                Models.ScheduleStatus.Pending   => (Brush)resources["TextPrimaryBrush"],
                Models.ScheduleStatus.Scheduled => (Brush)resources["PrimaryBrush"],
                Models.ScheduleStatus.Completed => (Brush)resources["SuccessBrush"],
                Models.ScheduleStatus.Released  => (Brush)resources["SecondaryBrush"],
                Models.ScheduleStatus.Cancelled => (Brush)resources["ErrorBrush"],
                _                               => (Brush)resources["IdleBrush"],
            } : (Brush)resources["IdleBrush"];
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class ScheduleStatusToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resources = Application.Current.Resources;
            var key = value is Models.ScheduleStatus s ? s switch
            {
                Models.ScheduleStatus.Pending   => "PioStatusPending",
                Models.ScheduleStatus.Scheduled => "PioStatusScheduled",
                Models.ScheduleStatus.Completed => "PioStatusCompleted",
                Models.ScheduleStatus.Released  => "PioStatusReleased",
                Models.ScheduleStatus.Cancelled => "PioStatusCancelled",
                _                               => null,
            } : null;
            return key != null && resources[key] is string label ? label : value?.ToString() ?? string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class ScheduleViewFilterToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resources = Application.Current.Resources;
            var key = value is ViewModels.ScheduleViewFilter f ? f switch
            {
                ViewModels.ScheduleViewFilter.AllActive      => "SchFilterAllActive",
                ViewModels.ScheduleViewFilter.Pending        => "SchFilterPending",
                ViewModels.ScheduleViewFilter.Scheduled      => "SchFilterScheduled",
                ViewModels.ScheduleViewFilter.ProductionDone => "SchFilterProductionDone",
                ViewModels.ScheduleViewFilter.Separator      => "SchFilterSeparator",
                ViewModels.ScheduleViewFilter.Completed      => "SchFilterCompleted",
                ViewModels.ScheduleViewFilter.Released       => "SchFilterReleased",
                ViewModels.ScheduleViewFilter.Cancelled      => "SchFilterCancelled",
                _                                            => null,
            } : null;
            return key != null && resources[key] is string label ? label : value?.ToString() ?? string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class ScheduleCardColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resources = Application.Current.Resources;
            return value is ViewModels.ScheduleCardColor color ? color switch
            {
                ViewModels.ScheduleCardColor.Green  => (Brush)resources["SuccessBrush"],
                ViewModels.ScheduleCardColor.Orange => (Brush)resources["WarningBrush"],
                ViewModels.ScheduleCardColor.Blue   => (Brush)resources["PrimaryBrush"],
                ViewModels.ScheduleCardColor.Gray   => (Brush)resources["BorderBrush"],
                _                                   => (Brush)resources["BorderBrush"],
            } : (Brush)resources["BorderBrush"];
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class ScheduleCardLoadLevelToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resources = Application.Current.Resources;
            var key = value is ViewModels.ScheduleCardLoadLevel level ? level switch
            {
                ViewModels.ScheduleCardLoadLevel.Low  => "SchCardLoadLow",
                ViewModels.ScheduleCardLoadLevel.Mid  => "SchCardLoadMid",
                ViewModels.ScheduleCardLoadLevel.High => "SchCardLoadHigh",
                _                                     => null,
            } : null;
            return key != null && resources[key] is string label ? label : string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class OrderProductionStatusToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Models.OrderProductionStatus s ? s switch
            {
                Models.OrderProductionStatus.Pending       => Properties.Resources.SchOrderStatusPending,
                Models.OrderProductionStatus.InProduction  => Properties.Resources.SchOrderStatusInProduction,
                Models.OrderProductionStatus.Completed     => Properties.Resources.SchOrderStatusCompleted,
                Models.OrderProductionStatus.Cancelled     => Properties.Resources.SchOrderStatusCancelled,
                _                                          => value.ToString() ?? string.Empty,
            } : string.Empty;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public static class ListBoxBehavior
    {
        public static readonly DependencyProperty AutoScrollToEndProperty = DependencyProperty.RegisterAttached(
                "AutoScrollToEnd",
                typeof(bool),
                typeof(ListBoxBehavior),
                new PropertyMetadata(false, OnAutoScrollToEndChanged));

        public static bool GetAutoScrollToEnd(DependencyObject obj)
            => (bool)obj.GetValue(AutoScrollToEndProperty);
        public static void SetAutoScrollToEnd(DependencyObject obj, bool value)
            => obj.SetValue(AutoScrollToEndProperty, value);
        private static void OnAutoScrollToEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox)
            {
                if ((bool)e.NewValue)
                {
                    // 確保在 Loaded / DataContextChanged 時重新檢查 ItemsSource
                    listBox.Loaded += (s, ev) => TryAttach(listBox);
                    listBox.DataContextChanged += (s, ev) => TryAttach(listBox);
                    TryAttach(listBox);
                }
                else
                {
                    TryDetach(listBox);
                }
            }
        }
        private static void TryAttach(ListBox listBox)
        {
            if (listBox.ItemsSource is INotifyCollectionChanged collection)
            {
                // 避免重複綁定
                collection.CollectionChanged -= CollectionChanged;
                collection.CollectionChanged += CollectionChanged;
            }
        }

        private static void TryDetach(ListBox listBox)
        {
            if (listBox.ItemsSource is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged -= CollectionChanged;
            }
        }
        private static void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            if (sender is IEnumerable && args.Action == NotifyCollectionChangedAction.Add)
            {
                // 找到對應的 ListBox
                foreach (Window window in Application.Current.Windows)
                {
                    foreach (var listBox in FindVisualChildren<ListBox>(window))
                    {
                        if (listBox.ItemsSource == sender && GetAutoScrollToEnd(listBox))
                        {
                            var lastItem = listBox.Items[listBox.Items.Count - 1];
                            listBox.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                listBox.ScrollIntoView(lastItem);
                                listBox.SelectedIndex = listBox.Items.Count - 1;
                            }));
                        }
                    }
                }
            }
        }
        // 輔助方法：搜尋 VisualTree
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
    public static class DataGridSelectedItemsBehavior
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IList),
                typeof(DataGridSelectedItemsBehavior),
                new PropertyMetadata(null, OnSelectedItemsChanged));

        public static void SetSelectedItems(DependencyObject element, IList value)
            => element.SetValue(SelectedItemsProperty, value);

        public static IList GetSelectedItems(DependencyObject element)
            => (IList)element.GetValue(SelectedItemsProperty);

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid grid)
            {
                grid.SelectionChanged += (s, args) =>
                {
                    var list = GetSelectedItems(grid);
                    list?.Clear();
                    foreach (var item in grid.SelectedItems)
                    {
                        list?.Add(item);
                    }
                };
            }
        }
    }
}
