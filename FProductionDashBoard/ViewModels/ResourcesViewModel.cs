using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Properties;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace FProductionDashBoard
{
    #region -- Log View Model --
    public class LogEntry
    {
        public string Message { get; set; } = string.Empty;
        public LogLevel Level { get; set; } = LogLevel.Info;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public object Color { get; set; } = "Black";
        public PackIconKind Icon { get; set; }
    }
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Success,
        Processing
    }
    public class LogService : ObservableObject
    {
        public ObservableCollection<LogEntry> Logs { get; } = new();
        public ObservableCollection<string> AvailableLogFiles { get; } = new();

        private readonly int _daysToKeep = 7;
        private readonly string _logDirectory = "Logs";
        private string GetLogFilePath()
        {
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(_logDirectory, $"logs_{date}.txt");
        }
        public LogService()
        {
            if (!Directory.Exists(_logDirectory))
            { Directory.CreateDirectory(_logDirectory); }
            //CleanupOldLogs();
        }

        public void AddLog(string message, LogLevel level = LogLevel.Info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var resources = Application.Current.Resources;
                var tocolor = level switch
                {
                    LogLevel.Success => (Brush)resources["SuccessColor"],
                    LogLevel.Warning => (Brush)resources["AlertColor"],
                    LogLevel.Error => (Brush)resources["ErrorColor"],
                    LogLevel.Info => (Brush)resources["InfoColor"],
                    LogLevel.Processing => (Brush)resources["ProcessingColor"],
                    _ => (Brush)resources["IdleColor"]
                };
                var toicon = level switch
                {
                    LogLevel.Success => PackIconKind.CheckCircle,
                    LogLevel.Warning => PackIconKind.AlertOutline,
                    LogLevel.Error => PackIconKind.Error,
                    LogLevel.Info => PackIconKind.Notebook,
                    LogLevel.Processing => PackIconKind.ProgressClock,
                    _ => PackIconKind.AlertOutline
                };

                var entry = new LogEntry
                {
                    Message = message,
                    Level = level,
                    Timestamp = DateTime.Now,
                    Color = tocolor,
                    Icon = toicon
                };
                Logs.Add(entry);

                // 同步到檔案
                //AppendLogToFile(entry);

                // 每次新增時檢查是否需要清理
                //CleanupOldLogs();
            }, DispatcherPriority.Background);
        }

        private void AppendLogToFile(LogEntry entry)
        {
            string filePath = GetLogFilePath();
            var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Message}";
            File.AppendAllText(filePath, line + Environment.NewLine);
        }
        private void CleanupOldLogs()
        {
            var files = Directory.GetFiles(_logDirectory, "logs_*.txt")
                                 .Select(f => new FileInfo(f))
                                 .OrderByDescending(f => f.CreationTime)
                                 .ToList();

            if (files.Count > _daysToKeep)
            {
                foreach (var oldFile in files.Skip(_daysToKeep))
                {
                    try
                    {
                        string zipName = Path.Combine(_logDirectory, $"{oldFile.Name.Replace(".txt", ".zip")}");
                        using (var zip = ZipFile.Open(zipName, ZipArchiveMode.Create))
                        {
                            zip.CreateEntryFromFile(oldFile.FullName, oldFile.Name);
                        }

                        oldFile.Delete();
                    }
                    catch
                    {
                        // 忽略刪除失敗
                    }
                }
            }
        }
        public void RefreshAvailableLogFiles()
        {
            AvailableLogFiles.Clear();
            foreach (var file in Directory.GetFiles(_logDirectory, "logs_*.txt"))
            {
                AvailableLogFiles.Add(Path.GetFileName(file));
            }
        }
        public string LoadLogFile(string fileName)
        {
            string filePath = Path.Combine(_logDirectory, fileName);
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            return "檔案不存在或已被壓縮備份。";
        }
    }
    #endregion

    #region -- Data Storage --
    public static class DataStorageService
    {

        private static readonly string filePath = "devices.json";

        public static void SaveDevices(ObservableCollection<DeviceInfo> devices)
        {
            var json = JsonSerializer.Serialize(devices);
            File.WriteAllText(filePath, json);
        }

        public static ObservableCollection<DeviceInfo> LoadDevices()
        {
            if (!File.Exists(filePath))
                return new ObservableCollection<DeviceInfo>();

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ObservableCollection<DeviceInfo>>(json)
                   ?? new ObservableCollection<DeviceInfo>();
        }


    }


    #endregion

    #region -- Converter/Behavior --
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOn = (bool)value;
            return isOn ? Application.Current.Resources["SuccessColor"] : Application.Current.Resources["ErrorColor"];
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotImplementedException(); }
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
                0 => (Brush)resources["SuccessColor"],
                1 => (Brush)resources["WarningColor"],
                2 => (Brush)resources["ErrorColor"],
                _ => (Brush)resources["IdleColor"]
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
    public class CollapseWidthConverter : IValueConverter
    {
        public double CollapsedWidth { get; set; } = 50;
        public double ExpandedWidth { get; set; } = 200;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isCollapsed = (bool)value;
            return isCollapsed ? CollapsedWidth : ExpandedWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        { throw new NotImplementedException(); }
    }
    public static class ListBoxBehavior
    {
        public static readonly DependencyProperty AutoScrollToEndProperty =
            DependencyProperty.RegisterAttached(
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
            if (d is ListBox listBox && (bool)e.NewValue)
            {
                // 確保在 Loaded / DataContextChanged 時重新檢查 ItemsSource
                listBox.Loaded += (s, ev) => TryAttach(listBox);
                listBox.DataContextChanged += (s, ev) => TryAttach(listBox);
            }
        }
        private static void TryAttach(ListBox listBox)
        {
            if (listBox.ItemsSource is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += (s, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Add)
                    {
                        var lastItem = listBox.Items[listBox.Items.Count - 1];
                        listBox.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            listBox.ScrollIntoView(lastItem);
                            listBox.SelectedIndex = listBox.Items.Count - 1; // 可選擇是否需要反白
                        }));
                    }
                };
            }
        }
    }

    #endregion


}
