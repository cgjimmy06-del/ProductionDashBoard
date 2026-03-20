using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Diagnostics;

namespace FProductionDashBoard.Repositories
{
    public interface IDataService
    {


    }

    public class SqlService : IDataService
    {
        public IDeviceRepository DeviceRepo { get; }
        public IUserRepository WorkerRepo { get; }

        public SqlService(IDeviceRepository devicerepo, IUserRepository workerrepo) 
        {
            DeviceRepo = devicerepo;
            WorkerRepo = workerrepo;

        }

    }

    public static class LoginService
    {
        public static bool checkConnection(string serverKey)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connStr = config.GetConnectionString($"{serverKey}_MESDashboard");
            using var conn = new SqlConnection(connStr);

            try {
                conn.Open(); return true;
            } catch (SqlException sqlex) {
                Debug.WriteLine($"SQL error: {sqlex.Message}"); return false; throw;
            } catch (TaskCanceledException) {
                Debug.WriteLine("Task Canceled"); return false; throw;
            } catch(Exception normalex) { 
                Debug.WriteLine($"error: {normalex.Message}"); return false; throw; }
        }

        public static UserInfo? validateUser(string serverKey, string userid, string password)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connStr = config.GetConnectionString($"{serverKey}_MESDashboard");
            var sqlStr = "SELECT user_id, name, permission FROM employee WHERE user_id=@Userid AND password=@Password";
            
            using var conn = new SqlConnection(connStr);
            return conn.QueryFirstOrDefault<UserInfo>(sqlStr, new { Userid = userid, Password = password });
        }
    }

}
