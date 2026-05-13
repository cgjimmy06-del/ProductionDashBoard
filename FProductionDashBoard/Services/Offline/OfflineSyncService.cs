using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Offline.Handlers;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace FProductionDashBoard.Services.Offline
{
    public class OfflineSyncService : IOfflineSyncService
    {
        private readonly IOfflineCacheService _cache;
        private readonly IServiceScopeFactory _scopeFactory;

        public OfflineSyncService(IOfflineCacheService cache, IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        public async Task<SyncResult> SyncPendingAsync()
        {
            if (!await _cache.HasPendingAsync().ConfigureAwait(false)) return SyncResult.Empty;

            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var equipmentRep = sp.GetRequiredService<IEquipmentRepository>();
            var handlers = sp.GetServices<IPendingOperationHandler>();

            if (!await equipmentRep.CheckConnectionAsync().ConfigureAwait(false)) return new SyncResult(0, 0);

            var pending = await _cache.GetPendingAsync().ConfigureAwait(false);

            int syncedCount = 0, failedCount = 0;
            foreach (var op in pending)
            {
                var handler = handlers.FirstOrDefault(h => h.OperationType == op.OperationType);
                if (handler is null) continue;

                try
                {
                    await handler.HandleAsync(op).ConfigureAwait(false);
                    await _cache.MarkSyncedAsync(op.Id).ConfigureAwait(false);
                    syncedCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SyncPendingAsync] OperationType={op.OperationType} Id={op.Id}: {ex.Message}");
                    await _cache.MarkFailedAsync(op.Id).ConfigureAwait(false);
                    failedCount++;
                }
            }

            return new SyncResult(syncedCount, failedCount);
        }
    }
}
