using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Offline.Payloads;
using System.Text.Json;

namespace FProductionDashBoard.Services.Offline.Handlers
{
    public class RoutineInspectionSyncHandler : IPendingOperationHandler
    {
        private readonly IInspectionRecordRepository _repo;

        public PendingOperationType OperationType => PendingOperationType.AddRoutineInspection;

        public RoutineInspectionSyncHandler(IInspectionRecordRepository repo) => _repo = repo;

        public async Task HandleAsync(PendingOperation op)
        {
            var payload = JsonSerializer.Deserialize<RoutineInspectionPayload>(op.PayloadJson)!;

            /// ���s�s�u�W�ǫe�A�T�{��Ʈw�O�_�w���ۦP���� *** �ثe�u�@����~�A�Q��k�ǤJ ***
            //var businessDay = payload.OperatedAt.Date;
            //bool exists = await _repo.ExistsInspectionInSlotAsync(
            //    payload.EquipmentId, payload.TimeSlotId, businessDay).ConfigureAwait(false);
            //if (exists) return;
            /// ���s�s�u�W�ǫe�A�T�{��Ʈw�O�_�w���ۦP���� *** �ثe�u�@����~�A�Q��k�ǤJ ***

            await _repo.AddInspectionRecordAsync(
                InspectionType.Routine, payload.EquipmentId, payload.EmployeeId,
                payload.Result, payload.TimeSlotId, payload.ProductId, payload.ErrorCode, payload.Description,
                operatedAt: payload.OperatedAt);
        }
    }
}
