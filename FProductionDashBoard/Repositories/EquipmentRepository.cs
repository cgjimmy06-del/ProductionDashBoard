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
        public EquipmentRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<IEnumerable<DeviceDto>> GetDevicesAllDapperAsync()
        {
            using (var connection = new SqlConnection(CurrectConnStr))
            {
                string sql = @" SELECT * FROM [dashboard_db].[dbo].[equipment]";

                return await connection.QueryAsync<DeviceDto>(sql, commandTimeout: 5).ConfigureAwait(false);
            }
        }

        public async Task<List<EquipmentType>> GetEquipmentTypesAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Set<EquipmentType>().ToListAsync().ConfigureAwait(false);
        }
    }
}
