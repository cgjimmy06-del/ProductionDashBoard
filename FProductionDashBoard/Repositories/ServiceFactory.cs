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
                conn.Open();
                return true;
            } catch { return false; }
        }

        public static UserInfo? validateUser(string serverKey, string userid, string password)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connStr = config.GetConnectionString($"{serverKey}_MESDashboard");
            using var conn = new SqlConnection(connStr);
            
            var sql = "SELECT user_id, name, permission FROM employee WHERE user_id=@Userid AND password=@Password";
            return conn.QueryFirstOrDefault<UserInfo>(sql, new { Userid = userid, Password = password });
        }
    }

}
