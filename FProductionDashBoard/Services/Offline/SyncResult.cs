namespace FProductionDashBoard.Services.Offline
{
    public record SyncResult(int SyncedCount, int FailedCount)
    {
        public static SyncResult Empty => new(0, 0);
    }
}
