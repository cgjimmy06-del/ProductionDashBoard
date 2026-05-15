using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        private bool isNewErrorLog = false;
        public ObservableCollection<string> AvailableLogFiles { get; } = new();

        public bool SaveToFile { get; set; } = false;
        public int DaysToKeep { get; set; } = 7;

        private readonly SemaphoreSlim _fileWriteLock = new(1, 1);
        private const string _archiveSubDir = "archive";
        private readonly string _logDirectory = "Logs";
        private readonly string _logFileName = "logs";
        private readonly string _errorLogFileName = "elogs";

        public string LogDirectory => Path.Combine(AppContext.BaseDirectory, _logDirectory);

        public LogService()
        {
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
        }

        public void RefreshAvailableLogFiles()
        {
            var files = Directory.GetFiles(_logDirectory, "*logs_*.txt")
                                 .Select(Path.GetFileName)
                                 .OrderByDescending(f => f)
                                 .ToList();
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                AvailableLogFiles.Clear();
                foreach (var f in files) AvailableLogFiles.Add(f!);
            });
        }

        public string GetLogFilePath(string fileName) =>
            Path.Combine(AppContext.BaseDirectory, _logDirectory, fileName);

        public void AddLog(string message, LogLevel level = LogLevel.Info)
        {
            var entry = new LogEntry { Message = message, Level = level, Timestamp = DateTime.Now };

            if (SaveToFile)
            {
                _ = AppendLogToFileAsync(entry, _logFileName);
                _ = Task.Run(() => CleanupOldLogs(_logFileName));
            }

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var resources = Application.Current.Resources;
                entry.Color = level switch
                {
                    LogLevel.Success => (Brush)resources["SuccessBrush"],
                    LogLevel.Warning => (Brush)resources["AlertBrush"],
                    LogLevel.Error => (Brush)resources["ErrorBrush"],
                    LogLevel.Info => (Brush)resources["InfoBrush"],
                    LogLevel.Processing => (Brush)resources["ProcessingBrush"],
                    _ => (Brush)resources["IdleBrush"]
                };
                entry.Icon = level switch
                {
                    LogLevel.Success => PackIconKind.CheckCircle,
                    LogLevel.Warning => PackIconKind.AlertOutline,
                    LogLevel.Error => PackIconKind.Error,
                    LogLevel.Info => PackIconKind.Notebook,
                    LogLevel.Processing => PackIconKind.ProgressClock,
                    _ => PackIconKind.AlertOutline
                };
                Logs.Add(entry);
            }, DispatcherPriority.Background);
        }

        public void AddErrorLog(string message)
        {
            var entry = new LogEntry { Message = message, Level = LogLevel.Error, Timestamp = DateTime.Now };

            if (SaveToFile)
            {
                _ = AppendLogToFileAsync(entry, _errorLogFileName, false);
                _ = Task.Run(() => CleanupOldLogs(_errorLogFileName));
            }

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var resources = Application.Current.Resources;
                entry.Color = (Brush)resources["ErrorBrush"];
                entry.Icon = PackIconKind.Error;
                ErrorLogs.Add(entry);
                IsNewErrorLog = true;
            }, DispatcherPriority.Background);
        }

        private string GetLogFilePathWithDate(string logtitle)
        {
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(_logDirectory, $"{logtitle}_{date}.txt");
        }

        private async Task AppendLogToFileAsync(LogEntry entry, string filetitle, bool showLevel = true)
        {
            string line = "";
            if (showLevel)
                line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Message}";
            else
                line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} {entry.Message}";

            await _fileWriteLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(GetLogFilePathWithDate(filetitle), line + Environment.NewLine);
            }
            finally
            {
                _fileWriteLock.Release();
            }
        }

        private void CleanupOldLogs(string filetitle)
        {
            var files = Directory.GetFiles(_logDirectory, $"{filetitle}_*.txt")
                                 .Select(f => new FileInfo(f))
                                 .OrderByDescending(f => f.CreationTime)
                                 .ToList();

            if (files.Count <= DaysToKeep) return;

            var archiveDir = Path.Combine(_logDirectory, _archiveSubDir);
            Directory.CreateDirectory(archiveDir);
            foreach (var oldFile in files.Skip(DaysToKeep))
            {
                try
                {
                    var zipPath = Path.Combine(archiveDir, oldFile.Name.Replace(".txt", ".zip"));
                    using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                    zip.CreateEntryFromFile(oldFile.FullName, oldFile.Name);
                    oldFile.Delete();
                }
                catch { }
            }
        }

        public async Task ExportInMemoryLogsAsync()
        {
            string currenttime = DateTime.Now.ToString("yy-MM-dd_HHmm");
            var logFilePath = Path.Combine(_logDirectory, $"log_{currenttime}_t.txt");
            var errorlogFilePath = Path.Combine(_logDirectory, $"elog_{currenttime}_t.txt");

            var lines = Logs.Select(e => $"{e.Timestamp:yy-MM-dd HH:mm:ss} [{e.Level}] {e.Message}").ToList();
            var errorlines = ErrorLogs.Select(e => $"[{e.Timestamp:yy-MM-dd HH:mm:ss}] {e.Message}").ToList();

            await Task.WhenAll(
                File.WriteAllLinesAsync(logFilePath, lines),
                File.WriteAllLinesAsync(errorlogFilePath, errorlines)
            ).ConfigureAwait(false);
        }
    }
    #endregion
}
