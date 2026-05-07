using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Properties;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Concurrent;
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

namespace FProductionDashBoard.Services
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
    public partial class LogService : ObservableObject
    {
        public ObservableCollection<LogEntry> Logs { get; } = new();
        public ObservableCollection<LogEntry> ErrorLogs { get; } = new();
        [ObservableProperty]
        private bool isNewErrorLog = false; // 是否有新異常紀錄
        public ObservableCollection<string> AvailableLogFiles { get; } = new();

        public bool SaveToFile = false;
        public int _daysToKeep = 3;
        private readonly string _logDirectory = "Logs"; // log路徑
        private readonly string _logFileName = "logs"; // log檔名 (接日期)
        private readonly string _errorLogFileName = "errorlogs"; // log檔名 (接日期)

        public LogService()
        {
            if (!Directory.Exists(_logDirectory))
            { Directory.CreateDirectory(_logDirectory); }
            //CleanupOldLogs();
        }
        public void RefreshAvailableLogFiles()
        {
            AvailableLogFiles.Clear();
            foreach (var file in Directory.GetFiles(_logDirectory, "*logs_*.txt"))
            {
                AvailableLogFiles.Add(Path.GetFileName(file));
            }
        }
        public void AddLog(string message, LogLevel level = LogLevel.Info)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
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

                // 是否同步到檔案
                if (SaveToFile)
                {
                    // _ = AppendLogToFileAsync(entry, _logFileName); // _ = 表示 fire-and-forget
                    AppendLogToFile(entry, _logFileName);
                    CleanupOldLogs(_logFileName);// 檢查清理 (daystokeep)
                }
            }, DispatcherPriority.Background);
        }
        public void AddErrorLog(string message)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var resources = Application.Current.Resources;
                var entry = new LogEntry
                {
                    Message = message,
                    Level = LogLevel.Error,
                    Timestamp = DateTime.Now,
                    Color = (Brush)resources["ErrorBrush"],
                    Icon = PackIconKind.Error
                };

                ErrorLogs.Add(entry);
                IsNewErrorLog = true;

                // 是否同步到檔案
                if (SaveToFile)
                {
                    AppendLogToFile(entry, _errorLogFileName);
                    CleanupOldLogs(_errorLogFileName);
                }
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
        private void CleanupOldLogs(string filetitle)
        {
            var files = Directory.GetFiles(_logDirectory, "{filetitle}_*.txt")
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
        public async Task SaveAllLogsToFileAsync()
        {
            string currenttime = DateTime.Now.ToString("yy-MM-dd_HHmm");
            
            var logFilePath = Path.Combine(_logDirectory, $"log_{currenttime}_t.txt");
            var errorlogFilePath = Path.Combine(_logDirectory, $"elog_{currenttime}_t.txt");

            // 把所有 Logs 轉成字串
            var lines = Logs.Select(entry =>
                $"{entry.Timestamp:yy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Message}");
            var errorlines = ErrorLogs.Select(entry =>
                $"[{entry.Timestamp:yy-MM-dd HH:mm:ss}] {entry.Message}");

            // 非同步寫入檔案（覆蓋舊檔）
            var task1 = File.WriteAllLinesAsync(logFilePath, lines);
            var task2 = File.WriteAllLinesAsync(errorlogFilePath, errorlines);

            await Task.WhenAll(task1, task2).ConfigureAwait(false);
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
        private void FlushLogs()
        {
            ConcurrentQueue<LogEntry> _logQueue = new(); // 全域
            int _batchSize = 50; // 全域

            var batch = new List<string>();

            while (_logQueue.TryDequeue(out var entry))
            {
                batch.Add($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Message}");

                if (batch.Count >= _batchSize)
                {
                    File.AppendAllLines(GetLogFilePathWithDate(_logFileName), batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                File.AppendAllLines(GetLogFilePathWithDate(_logFileName), batch);
            }
        }
    }
    #endregion

}
