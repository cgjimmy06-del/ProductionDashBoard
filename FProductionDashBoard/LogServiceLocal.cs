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
        public ObservableCollection<LogEntry> ErrorLogs { get; } = new();
        public ObservableCollection<string> AvailableLogFiles { get; } = new();

        private readonly int _daysToKeep = 3;
        private readonly string _logDirectory = "Logs"; // log路徑
        private readonly string _logFileName = "logs"; // log檔名 (接日期)
        private readonly string _errorLogFileName = "errorlogs"; // log檔名 (接日期)

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
                    LogLevel.Success => (Brush)resources["SuccessBrush"],
                    LogLevel.Warning => (Brush)resources["AlertBrush"],
                    LogLevel.Error => (Brush)resources["ErrorBrush"],
                    LogLevel.Info => (Brush)resources["InfoBrush"],
                    LogLevel.Processing => (Brush)resources["ProcessingBrush"],
                    _ => (Brush)resources["IdleBrush"]
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
                //AppendLogToFile(entry, _logFileName);
                // 每次新增時檢查是否需要清理
                //CleanupOldLogs();

            }, DispatcherPriority.Background);
        }
        public void AddErrorLog(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var resources = Application.Current.Resources;
                var entry = new LogEntry
                {
                    Message = message,
                    Level = LogLevel.Error,
                    Timestamp = DateTime.Now,
                    Color = (Brush)resources["InfoBrush"],
                    Icon = PackIconKind.Error
                };

                ErrorLogs.Add(entry);
                // 同步到檔案
                //AppendLogToFile(entry, _errorLogFileName);
                // 每次新增時檢查是否需要清理
                //CleanupOldLogs();

            }, DispatcherPriority.Background);
        }
        private string GetLogFilePathWithDate(string logtitle)
        {
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(_logDirectory, $"{logtitle}_{date}.txt");
        }
        private void AppendLogToFile(LogEntry entry, string filetitle)
        {
            string filePath = GetLogFilePathWithDate(filetitle);
            var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Message}";
            File.AppendAllText(filePath, line + Environment.NewLine);
        }

        // 同時清除與更新logs與errorlogs
        private void CleanupOldLogs()
        {
            var files = Directory.GetFiles(_logDirectory, "*logs_*.txt")
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
            foreach (var file in Directory.GetFiles(_logDirectory, "*logs_*.txt"))
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

        public static void SaveDevices(ObservableCollection<Models.DeviceInfo> devices)
        {
            var json = JsonSerializer.Serialize(devices);
            File.WriteAllText(filePath, json);
        }

        public static ObservableCollection<Models.DeviceInfo> LoadDevices()
        {
            if (!File.Exists(filePath))
                return new ObservableCollection<Models.DeviceInfo>();

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ObservableCollection<Models.DeviceInfo>>(json)
                   ?? new ObservableCollection<Models.DeviceInfo>();
        }


    }


    #endregion
}
