using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Offline.Payloads;
using System.Text.Json;

namespace FProductionDashBoard.Services.Offline.Handlers
{
    public class FirstInspectionSyncHandler : IPendingOperationHandler
    {
        private readonly IInspectionRecordRepository _repo;

        public PendingOperationType OperationType => PendingOperationType.AddFirstInspection;

        public FirstInspectionSyncHandler(IInspectionRecordRepository repo) => _repo = repo;

        public async Task HandleAsync(PendingOperation op)
        {
            var payload = JsonSerializer.Deserialize<FirstInspectionPayload>(op.PayloadJson)!;
            await _repo.AddInspectionRecordAsync(
                InspectionType.First, payload.EquipmentId, payload.EmployeeId,
                payload.Result, null, payload.Product, payload.ErrorCode, payload.Description,
                operatedAt: payload.OperatedAt);
        }
    }
}
