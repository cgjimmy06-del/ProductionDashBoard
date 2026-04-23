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
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Database.CanConnectAsync();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Set<T>().ToListAsync();
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Set<T>().FindAsync(id);
        }

        public async Task AddAsync(T entity)
        {
            await using var ctx = _factory.CreateDbContext();
            ctx.Set<T>().Add(entity);
            await ctx.SaveChangesAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            await using var ctx = _factory.CreateDbContext();
            ctx.Set<T>().Update(entity);
            await ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var ctx = _factory.CreateDbContext();
            var entity = await ctx.Set<T>().FindAsync(id);
            if (entity != null)
            {
                ctx.Set<T>().Remove(entity);
                await ctx.SaveChangesAsync();
            }
        }
    }
}
