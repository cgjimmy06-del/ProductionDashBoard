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
            { return isOn ? Application.Current.Resources["SuccessBrush"] : Application.Current.Resources["PrimaryBrush"]; }

            if (parameter?.ToString() == "Invert")
                isOn = !isOn;

            return isOn ? Application.Current.Resources["SuccessBrush"] : Application.Current.Resources["ErrorBrush"];
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
