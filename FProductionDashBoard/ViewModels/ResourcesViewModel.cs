using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FProductionDashBoard
{


    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOn = (bool)value;
            return isOn ? Brushes.Green : Brushes.Red;
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



    //public class RelayCommand : ICommand
    //{
    //    private readonly Action _execute;
    //    private readonly Func<bool> _canExecute;
    //    public RelayCommand(Action execute, Func<bool> canExecute = null)
    //    {
    //        _execute = execute; _canExecute = canExecute;
    //    }
    //    public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
    //    public void Execute(object parameter) => _execute();
    //    public event EventHandler CanExecuteChanged
    //    {
    //        add => CommandManager.RequerySuggested += value;
    //        remove => CommandManager.RequerySuggested -= value;
    //    }
    //}

}
