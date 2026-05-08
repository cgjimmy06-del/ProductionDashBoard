using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Offline.Payloads;
using System.Text.Json;

namespace FProductionDashBoard.Services.Offline.Handlers
{
    public class ReplacementSyncHandler : IPendingOperationHandler
    {
        private readonly IMaterialReplacementRepository _repo;

        public PendingOperationType OperationType => PendingOperationType.AddReplacement;

        public ReplacementSyncHandler(IMaterialReplacementRepository repo) => _repo = repo;

        public async Task HandleAsync(PendingOperation op)
        {
            var payload = JsonSerializer.Deserialize<ReplacementPayload>(op.PayloadJson)!;
            var details = payload.Materials
                .Select(m => (m.MaterialId, m.Quantity))
                .ToList();
            await _repo.AddReplacementRecordAsync(payload.EquipmentId, payload.EmployeeId, "MTRP0001", details, payload.OperatedAt).ConfigureAwait(false);
        }
    }
}
