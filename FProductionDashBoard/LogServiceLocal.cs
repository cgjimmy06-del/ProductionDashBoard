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
using System.Security.Cryptography;
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
            Application.Current.Dispatcher.Invoke(() =>
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

            await Task.WhenAll(task1, task2);
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

    #region -- Data Storage --
    public static class DataStorageService
    {
        //private static readonly string baseFolder = Path.Combine(
        //    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        //    "FProductionDashBoard");
        private static readonly string baseFolder = "Settings";

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true, // 美化 JSON
            PropertyNameCaseInsensitive = true, // 忽略大小寫
            AllowTrailingCommas = true // 容忍尾逗號
        };

        /// <summary>
        /// 同步儲存
        /// </summary>
        public static void Save<T>(T data, string fileName)
        {
            try
            {
                EnsureFolderExists();
                string filePath = Path.Combine(baseFolder, fileName);
                var json = JsonSerializer.Serialize(data, jsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Save Error] {ex.Message}");
            }
        }

        /// <summary>
        /// 同步載入
        /// </summary>
        public static T Load<T>(string fileName) where T : new()
        {
            try
            {
                EnsureFolderExists();
                string filePath = Path.Combine(baseFolder, fileName);
                if (!File.Exists(filePath))
                    return new T();

                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<T>(json, jsonOptions) ?? new T();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Load Error] {ex.Message}");
                return new T();
            }
        }

        /// <summary>
        /// 非同步儲存 (可取消)
        /// </summary>
        public static async Task SaveAsync<T>(T data, string fileName, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureFolderExists();
                string filePath = Path.Combine(baseFolder, fileName);
                var json = JsonSerializer.Serialize(data, jsonOptions);
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[SaveAsync] Operation was cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveAsync Error] {ex.Message}");
            }
        }

        /// <summary>
        /// 非同步載入 (可取消)
        /// </summary>
        public static async Task<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default) where T : new()
        {
            try
            {
                EnsureFolderExists();
                string filePath = Path.Combine(baseFolder, fileName);
                if (!File.Exists(filePath))
                    return new T();

                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return JsonSerializer.Deserialize<T>(json, jsonOptions) ?? new T();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[LoadAsync] Operation was cancelled.");
                return new T();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LoadAsync Error] {ex.Message}");
                return new T();
            }
        }

        /// <summary>
        /// 確保資料夾存在
        /// </summary>
        private static void EnsureFolderExists()
        {
            if (!Directory.Exists(baseFolder))
                Directory.CreateDirectory(baseFolder);
        }
    }

    public static class EncryptionService
    {
        // 建議金鑰與 IV 從安全來源讀取 (例如環境變數或 Windows Credential Manager)
        private static readonly byte[] aesKey = Encoding.UTF8.GetBytes("Your32ByteLengthSecureKeyHere1234567890"); // 32 bytes for AES-256
        private static readonly byte[] aesIV = Encoding.UTF8.GetBytes("Your16ByteIVHere!"); // 16 bytes for AES

        static EncryptionService()
        {
            // 從環境變數讀取金鑰與 IV
            string? keyEnv = Environment.GetEnvironmentVariable("YOURAPP_AES_KEY");
            string? ivEnv = Environment.GetEnvironmentVariable("YOURAPP_AES_IV");

            if (string.IsNullOrEmpty(keyEnv) || string.IsNullOrEmpty(ivEnv))
                throw new InvalidOperationException("AES key/IV not found in environment variables.");

            aesKey = Encoding.UTF8.GetBytes(keyEnv);
            aesIV = Encoding.UTF8.GetBytes(ivEnv);

            if (aesKey.Length != 32) // AES-256 需要 32 bytes
                throw new InvalidOperationException("AES key must be 32 bytes.");
            if (aesIV.Length != 16) // IV 需要 16 bytes
                throw new InvalidOperationException("AES IV must be 16 bytes.");
        }

        /// <summary>
        /// AES 加密字串
        /// </summary>
        public static string EncryptString(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = aesIV;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        /// <summary>
        /// AES 解密字串
        /// </summary>
        public static string DecryptString(string cipherText)
        {
            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = aesIV;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var buffer = Convert.FromBase64String(cipherText);

            using var ms = new MemoryStream(buffer);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }
    public static class PasswordHasher
    {
        //public static string HashPassword(string password, byte[] salt)
        //{
        //    return Convert.ToBase64String(KeyDerivation.Pbkdf2(
        //        password: password,
        //        salt: salt,
        //        prf: KeyDerivationPrf.HMACSHA256,
        //        iterationCount: 10000,
        //        numBytesRequested: 32));
        //}

        //public static bool VerifyPassword(string password, byte[] salt, string hashedPassword)
        //{
        //    var hash = HashPassword(password, salt);
        //    return hash == hashedPassword;
        //}

        //public static byte[] GenerateSalt()
        //{
        //    var salt = new byte[16];
        //    using var rng = RandomNumberGenerator.Create();
        //    rng.GetBytes(salt);
        //    return salt;
        //}
    }
    #endregion
}
