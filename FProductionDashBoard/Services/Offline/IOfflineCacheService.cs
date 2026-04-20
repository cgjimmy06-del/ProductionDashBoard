namespace FProductionDashBoard.Services.Offline
{
    public interface IOfflineCacheService
    {
        Task EnqueueAsync(PendingOperation operation);
        Task<List<PendingOperation>> GetPendingAsync();
        Task MarkSyncedAsync(Guid id);
        Task MarkFailedAsync(Guid id, int maxRetry);
        Task<bool> HasPendingAsync();
    }
}
