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

namespace FProductionDashBoard.Repositories
{
    public class DeviceRepository : Repository<DeviceInfo, InfoDbContext>, IDeviceRepository
    {
        private readonly InfoDbContext _context;

        public DeviceRepository(InfoDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DeviceDto>> GetDevicesAllDapperAsync()
        {
            using (var connection = new SqlConnection(CurrectConnStr))
            {
                string sql = @" SELECT * FROM [MESInformation].[dbo].[MES_Device]";

                return await connection.QueryAsync<DeviceDto>(sql, commandTimeout: 5);
            }
        }


    }
}
