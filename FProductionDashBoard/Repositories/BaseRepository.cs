using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FProductionDashBoard.Repositories
{
    public interface IRepository<T, TContext> where T : class where TContext : DbContext
    {
        Task<bool> CheckConnectionAsync();

        public Task<IEnumerable<T>> GetAllAsync();
        public Task<T?> GetByIdAsync(int id);
        public Task AddAsync(T entity);
        public Task UpdateAsync(T entity);
        public Task DeleteAsync(int id);
    }

    public class Repository<T, TContext> : IRepository<T, TContext> where T : class where TContext : DbContext
    {
        protected readonly IDbContextFactory<TContext> _factory;

        public Repository(IDbContextFactory<TContext> factory)
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
                        SqlConnection.ClearPool(c); // Pool Manager 清理 broken connection 為 UI 凍結主因
                return result;
            }
            catch
            {
                using (var c = new SqlConnection(connStr))
                    SqlConnection.ClearPool(c);
                return false;
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Set<T>().ToListAsync().ConfigureAwait(false);
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Set<T>().FindAsync(id).ConfigureAwait(false);
        }

        public async Task AddAsync(T entity)
        {
            await using var ctx = _factory.CreateDbContext();
            ctx.Set<T>().Add(entity);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task UpdateAsync(T entity)
        {
            await using var ctx = _factory.CreateDbContext();
            ctx.Set<T>().Update(entity);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task DeleteAsync(int id)
        {
            await using var ctx = _factory.CreateDbContext();
            var entity = await ctx.Set<T>().FindAsync(id).ConfigureAwait(false);
            if (entity != null)
            {
                ctx.Set<T>().Remove(entity);
                await ctx.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
