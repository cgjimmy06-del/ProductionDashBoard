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

namespace FProductionDashBoard.Services
{
    public static class LoginDapper // 一般登入驗證 (純 Dapper 操作)
    {
        private static IConfiguration? _cachedConfig;

        internal static IConfiguration GetCachedConfig()
            => _cachedConfig ?? BuildConfig();
        private static IConfiguration BuildConfig()
        {
            if (_cachedConfig != null) return _cachedConfig;
            _cachedConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            return _cachedConfig;
        }
        public static bool checkConnection(string serverKey)
        {
            var config = GetCachedConfig();
            var connStr = config.GetConnectionString($"{serverKey}_MESDashboard");
            using var conn = new SqlConnection(connStr);

            try
            {
                conn.Open(); return true;
            }
            catch (SqlException sqlex)
            {
                Debug.WriteLine($"[checkConnection] SQL: {sqlex.Message}"); return false;
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("[checkConnection] Task cancelled"); return false;
            }
            catch (Exception normalex)
            {
                Debug.WriteLine($"[checkConnection] {normalex.Message}"); return false;
            }
        }
        public static UiModels.UserInfo? validateUser(string serverKey, string userid, string password)
        {
            var config = GetCachedConfig();
            var connStr = config.GetConnectionString($"{serverKey}_MESDashboard");
            var sqlStr = "SELECT employee_id, card_id, user_id, name, role_id, email FROM employee " +
                "WHERE user_id=@Userid AND password=@Password";

            using var conn = new SqlConnection(connStr);
            using var cmd = new SqlCommand(sqlStr, conn);
            cmd.Parameters.AddWithValue("@Userid", userid);
            cmd.Parameters.AddWithValue("@Password", password);

            conn.Open();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                // 手動對應，確保欄位正確
                var employeeId = reader["employee_id"] != DBNull.Value ? Convert.ToInt32(reader["employee_id"]) : 1;
                var cardId = reader["card_id"].ToString() ?? "";
                var userId = reader["user_id"].ToString() ?? "";
                var name = reader["name"].ToString() ?? "";
                var roleId = reader["role_id"] != DBNull.Value ? Convert.ToInt32(reader["role_id"]) : 1;
                var email = reader["email"].ToString() ?? "";

                return new UiModels.UserInfo
                {
                    UserId = userId,
                    Name = name,
                    RoleId = roleId,
                    Id = employeeId,
                    CardId = cardId,
                    Email = email
                };
            }
            return null;
            //return conn.QueryFirstOrDefault<UiModels.UserInfo>(sqlStr, new { Userid = userid, Password = password });
        }
    }
}
