using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
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
    }
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }
    public class LogViewModel : ObservableObject
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

        public LogViewModel()
        {
            if (!Directory.Exists(_logDirectory))
            { Directory.CreateDirectory(_logDirectory); }
            //CleanupOldLogs();
        }

        public void AddLog(string message, LogLevel level = LogLevel.Info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var entry = new LogEntry
                {
                    Message = message,
                    Level = level,
                    Timestamp = DateTime.Now
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

    #region -- Converter --
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

    #endregion


}
