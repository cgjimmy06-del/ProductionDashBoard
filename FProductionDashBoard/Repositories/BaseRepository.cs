using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FProductionDashBoard.Repositories
{
    public interface IRepository<T, TContext> where T : class where TContext : DbContext
    {
        public string CurrectConnStr { get; set; }
        bool CheckConnection();
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

        public string CurrectConnStr { get; set; } = "";

        public Repository(IDbContextFactory<TContext> factory)
        {
            _factory = factory;
            using var ctx = factory.CreateDbContext();
            CurrectConnStr = ctx.Database.GetDbConnection().ConnectionString;
        }

        public bool CheckConnection()
        {
            using var ctx = _factory.CreateDbContext();
            return ctx.Database.CanConnect();
        }

        public async Task<bool> CheckConnectionAsync()
        {
            using var cts = new CancellationTokenSource(2000);
            try
            {
                await using var ctx = _factory.CreateDbContext();
                return await ctx.Database.CanConnectAsync(cts.Token).ConfigureAwait(false);
            }
            catch { return false; }
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
