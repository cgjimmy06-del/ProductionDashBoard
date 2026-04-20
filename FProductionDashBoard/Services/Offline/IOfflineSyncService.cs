namespace FProductionDashBoard.Services.Offline
{
    public interface IOfflineSyncService
    {
        Task<SyncResult> SyncPendingAsync();
    }
}
