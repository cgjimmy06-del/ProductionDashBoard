namespace FProductionDashBoard.Services.Offline
{
    public enum PendingOperationType
    {
        AddReplacement,
        AddFirstInspection,
        AddRoutineInspection
    }

    public enum PendingStatus
    {
        Pending,
        Failed
    }

    public class PendingOperation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public PendingOperationType OperationType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int RetryCount { get; set; } = 0;
        public DateTime? LastAttemptAt { get; set; }
        public PendingStatus Status { get; set; } = PendingStatus.Pending;
        public string PayloadJson { get; set; } = string.Empty;
    }
}
