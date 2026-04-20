namespace FProductionDashBoard.Services.Offline
{
    public interface IOfflineSyncService
    {
        Task SyncPendingAsync();
    }
}
