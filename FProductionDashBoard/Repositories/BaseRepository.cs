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
        public TContext GetContext();

        public Task<IEnumerable<T>> GetAllAsync();
        public Task<T?> GetByIdAsync(int id);
        public Task AddAsync(T entity);
        public Task UpdateAsync(T entity);
        public Task DeleteAsync(int id);
    }

    public class Repository<T, TContext> : IRepository<T, TContext> where T : class where TContext : DbContext
    {
        protected readonly TContext _context;
        protected readonly DbSet<T> _dbSet;

        public string CurrectConnStr { get; set; } = "";

        public Repository(TContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();

            CurrectConnStr = _context.Database.GetDbConnection().ConnectionString;
        }

        public bool CheckConnection()
        { return _context.Database.CanConnect(); }
        public async Task<bool> CheckConnectionAsync()
            => await _context.Database.CanConnectAsync();
        public TContext GetContext() {  return _context; }

        public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();
        public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);
        public async Task AddAsync(T entity)
        {
            _dbSet.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
