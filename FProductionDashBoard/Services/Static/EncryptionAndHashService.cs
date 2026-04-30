using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
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
}
