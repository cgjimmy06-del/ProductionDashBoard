namespace FProductionDashBoard.Services.Offline.Handlers
{
    public interface IPendingOperationHandler
    {
        PendingOperationType OperationType { get; }
        Task HandleAsync(PendingOperation op);
    }
}
