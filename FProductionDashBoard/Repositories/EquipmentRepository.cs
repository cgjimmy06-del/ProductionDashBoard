using Dapper;
using FProductionDashBoard.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class EquipmentRepository : Repository<Equipment, MesDbContext>, IEquipmentRepository
    {
        public EquipmentRepository(MesDbContext context) : base(context)
        {
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
