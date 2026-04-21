namespace FProductionDashBoard.Services.Exceptions
{
    public class OfflineOperationQueuedException : Exception
    {
        public Guid PendingOperationId { get; }

        public OfflineOperationQueuedException(Guid pendingOperationId)
            : base("操作已暫存至本地，待連線恢復後自動上傳。")
        {
            PendingOperationId = pendingOperationId;
        }
    }
}
