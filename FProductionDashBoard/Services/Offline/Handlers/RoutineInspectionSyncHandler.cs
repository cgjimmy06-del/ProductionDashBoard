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

            /// 重新連線上傳前，確認資料庫是否已有相同紀錄 *** 目前工作日錯誤，想辦法傳入 ***
            //var businessDay = payload.OperatedAt.Date;
            //bool exists = await _repo.ExistsInspectionInSlotAsync(
            //    payload.EquipmentId, payload.TimeSlotId, businessDay).ConfigureAwait(false);
            //if (exists) return;
            /// 重新連線上傳前，確認資料庫是否已有相同紀錄 *** 目前工作日錯誤，想辦法傳入 ***

            await _repo.AddInspectionRecordAsync(
                InspectionType.Routine, payload.EquipmentId, payload.EmployeeId,
                payload.Result, payload.TimeSlotId, payload.Product, payload.ErrorCode, payload.Description,
                operatedAt: payload.OperatedAt);
        }
    }
}
