using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FProductionDashBoard.Repositories.IDeviceRepository;
using FProductionDashBoard.Models;

namespace FProductionDashBoard.Repositories
{
    public class DeviceRepository : Repository<DeviceInfo, MesDbContext>, IDeviceRepository
    {
        private readonly MesDbContext _context;

        public DeviceRepository(MesDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DeviceDto>> GetDevicesAllDapperAsync()
        {
            using (var connection = new SqlConnection(CurrectConnStr))
            {
                string sql = @" SELECT * FROM [dashboard_db].[dbo].[equipment]";

                return await connection.QueryAsync<DeviceDto>(sql, commandTimeout: 5);
            }
        }


    }
}
