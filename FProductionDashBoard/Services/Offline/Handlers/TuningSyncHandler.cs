using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Offline.Payloads;
using System.Text.Json;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services.Offline.Handlers
{
    public class TuningSyncHandler : IPendingOperationHandler
    {
        private readonly ITuningRecordRepository _repo;

        public PendingOperationType OperationType => PendingOperationType.AddTuning;

        public TuningSyncHandler(ITuningRecordRepository repo) => _repo = repo;

        public async Task HandleAsync(PendingOperation op)
        {
            var payload = JsonSerializer.Deserialize<TuningPayload>(op.PayloadJson)!;
            await _repo.AddTuningRecordAsync(
                payload.TuningType, payload.EquipmentId, payload.EmployeeId,
                payload.DurationSec, payload.Product, operatedAt: payload.OperatedAt);
        }
    }
}
