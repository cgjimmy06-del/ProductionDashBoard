using Dapper;
using FProductionDashBoard.Models.Extra;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories.ExtraDb
{
    public class DataDbRepository : IDataDbRepository
    {
        private readonly IDbContextFactory<DataDbContext> _factory;

        public DataDbRepository(IDbContextFactory<DataDbContext> factory) => _factory = factory;

        public async Task<IEnumerable<VwMesDailyProcessData>> GetDailyProcessDataAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            var conn = ctx.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync().ConfigureAwait(false);
            return await conn.QueryAsync<VwMesDailyProcessData>(
                "SELECT * FROM vw_MES_Daily_ProcessData").ConfigureAwait(false);
        }
    }
}
