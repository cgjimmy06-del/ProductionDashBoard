using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;

namespace FProductionDashBoard
{
    public static class DataStorageService // 後續須統一處理 try catch 的logging
    {
        // 存在使用者的AppData，後續可能需要
        //private static readonly string baseFolder = Path.Combine(
        //    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FProductionDashBoard");

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

    public static class EncryptionService // AES 加解密
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
    public static class PasswordHasher // 密碼雜湊
    {
        // 需安裝Nuget套件: Microsoft.AspNetCore.Cryptography.KeyDerivation
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
    public static class LoginService // 一般登入驗證 (純 Dapper 操作)
    {
        public static bool checkConnection(string serverKey)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connStr = config.GetConnectionString($"{serverKey}_MESDashboard");
            using var conn = new SqlConnection(connStr);

            try
            {
                conn.Open(); return true;
            }
            catch (SqlException sqlex)
            {
                Debug.WriteLine($"SQL error: {sqlex.Message}"); return false; throw;
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("Task Canceled"); return false; throw;
            }
            catch (Exception normalex)
            {
                Debug.WriteLine($"error: {normalex.Message}"); return false; throw;
            }
        }
        public static UiModels.UserInfo? validateUser(string serverKey, string userid, string password)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connStr = config.GetConnectionString($"{serverKey}_MESDashboard");
            var sqlStr = "SELECT user_id, name, permission FROM employee WHERE user_id=@Userid AND password=@Password";

            using var conn = new SqlConnection(connStr);
            return conn.QueryFirstOrDefault<UiModels.UserInfo>(sqlStr, new { Userid = userid, Password = password });
        }
    }
}
