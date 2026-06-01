using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Offline.Handlers;
using Microsoft.Extensions.DependencyInjection;

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
            var errors = new List<string>();
            foreach (var op in pending)
            {
                var handler = handlers.FirstOrDefault(h => h.OperationType == op.OperationType);
                if (handler is null) continue;

                bool handled = false; // true = HandleAsync 已寫入 DB，MarkSyncedAsync 失敗時仍會重試，有重複寫入風險
                try
                {
                    await handler.HandleAsync(op).ConfigureAwait(false);
                    handled = true;
                    await _cache.MarkSyncedAsync(op.Id).ConfigureAwait(false);
                    syncedCount++;
                }
                catch (Exception ex)
                {
                    await _cache.MarkFailedAsync(op.Id).ConfigureAwait(false);
                    failedCount++;
                    if (handled)
                        errors.Add($"[SyncPendingAsync] 操作已寫入 DB 但本地標記失敗（潛在重複寫入），OperationType={op.OperationType} Id={op.Id}: {ex.Message}");
                    else
                        errors.Add($"[SyncPendingAsync] OperationType={op.OperationType} Id={op.Id}: {ex.Message}");
                }
            }

            return new SyncResult(syncedCount, failedCount, errors);
        }
    }
}
