using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
    public static class JsonDataService
    {
        // 存在使用者的AppData，後續可能需要 ApplicationData, LocalApplicationData
        //private static readonly string baseFolder = Path.Combine(
        //    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FProductionDashBoard");

        private static readonly string baseFolder =
            Path.Combine(AppContext.BaseDirectory, "Settings");

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
                Debug.WriteLine($"[Save] {ex.Message}");
                throw;
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
                Debug.WriteLine($"[Load] {ex.Message}");
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
                await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[SaveAsync] Operation cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveAsync] {ex.Message}");
                throw;
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

                var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Deserialize<T>(json, jsonOptions) ?? new T();
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[LoadAsync] Operation cancelled");
                return new T();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadAsync] {ex.Message}");
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
}
