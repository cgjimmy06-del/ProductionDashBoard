using FProductionDashBoard.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FProductionDashBoard.Services.Offline
{
    public class OfflineCacheService : IOfflineCacheService
    {
        private readonly IDbContextFactory<LocalDbContext> _factory;

        public OfflineCacheService(IDbContextFactory<LocalDbContext> factory)
        {
            _factory = factory;
            using var db = _factory.CreateDbContext();
            db.Database.EnsureCreated();
        }

        public async Task EnqueueAsync(PendingOperation operation)
        {
            await using var db = _factory.CreateDbContext();
            await db.PendingOperations.AddAsync(operation).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<List<PendingOperation>> GetPendingAsync()
        {
            await using var db = _factory.CreateDbContext();
            return await db.PendingOperations
                .OrderBy(p => p.CreatedAt)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task MarkSyncedAsync(Guid id)
        {
            await using var db = _factory.CreateDbContext();
            var op = await db.PendingOperations.FindAsync(id).ConfigureAwait(false);
            if (op is not null)
            {
                db.PendingOperations.Remove(op);
                await db.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async Task MarkFailedAsync(Guid id)
        {
            await using var db = _factory.CreateDbContext();
            var op = await db.PendingOperations.FindAsync(id).ConfigureAwait(false);
            if (op is null) return;

            op.RetryCount++;
            op.LastAttemptAt = DateTime.Now;
            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<bool> HasPendingAsync()
        {
            await using var db = _factory.CreateDbContext();
            return await db.PendingOperations.AnyAsync().ConfigureAwait(false);
        }
    }
}
