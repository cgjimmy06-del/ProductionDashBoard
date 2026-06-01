namespace FProductionDashBoard.Services.Offline
{
    public record SyncResult(int SyncedCount, int FailedCount, IReadOnlyList<string>? Errors = null)
    {
        public static SyncResult Empty => new(0, 0);
    }
}
