using FProductionDashBoard.Models.Extra;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories.ExtraDb
{
    public class InfoDbRepository : IInfoDbRepository
    {
        private readonly IDbContextFactory<InfoDbContext> _factory;

        public InfoDbRepository(IDbContextFactory<InfoDbContext> factory)
        {
            _factory = factory;
        }

        private const int ConnectionTimeoutMs = 2000;

        public async Task<bool> CheckConnectionAsync()
        {
            using var cts = new CancellationTokenSource(ConnectionTimeoutMs);
            await using var ctx = _factory.CreateDbContext();
            var connStr = ctx.Database.GetConnectionString()!;
            try
            {
                var result = await ctx.Database.CanConnectAsync(cts.Token).ConfigureAwait(false);
                if (!result)
                    using (var c = new SqlConnection(connStr))
                        SqlConnection.ClearPool(c);
                return result;
            }
            catch
            {
                using (var c = new SqlConnection(connStr))
                    SqlConnection.ClearPool(c);
                return false;
            }
        }

        public async Task<string?> GetCustomerByMediumAsync(string mediumCode)
        {
            await using var ctx = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            return await ctx.MesCustomerCodes
                .Where(c => c.MediumCategories == mediumCode)
                .Select(c => c.Customer)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<MesDevice>> GetAllMesDevicesAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            return await ctx.MesDevices
                .AsNoTracking()
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task AddMesDeviceAsync(MesDevice entity)
        {
            await using var ctx = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            ctx.MesDevices.Add(entity);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task UpdateMesDeviceAsync(MesDevice entity)
        {
            await using var ctx = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            ctx.MesDevices.Update(entity);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
