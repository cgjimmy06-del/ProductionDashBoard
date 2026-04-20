using FProductionDashBoard.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FProductionDashBoard.Services.Offline
{
    public class OfflineCacheService : IOfflineCacheService
    {
        private readonly LocalDbContext _db;

        public OfflineCacheService(LocalDbContext db)
        {
            _db = db;
            _db.Database.EnsureCreated();
        }

        public async Task EnqueueAsync(PendingOperation operation)
        {
            await _db.PendingOperations.AddAsync(operation);
            await _db.SaveChangesAsync();
        }

        public async Task<List<PendingOperation>> GetPendingAsync()
        {
            return await _db.PendingOperations
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task MarkSyncedAsync(Guid id)
        {
            var op = await _db.PendingOperations.FindAsync(id);
            if (op is not null)
            {
                _db.PendingOperations.Remove(op);
                await _db.SaveChangesAsync();
            }
        }

        public async Task MarkFailedAsync(Guid id)
        {
            var op = await _db.PendingOperations.FindAsync(id);
            if (op is null) return;

            op.RetryCount++;
            op.LastAttemptAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        public async Task<bool> HasPendingAsync()
        {
            return await _db.PendingOperations.AnyAsync();
        }
    }
}
