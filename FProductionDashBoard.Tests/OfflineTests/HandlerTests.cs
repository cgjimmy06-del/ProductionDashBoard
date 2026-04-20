using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services.Offline.Handlers;
using FProductionDashBoard.Services.Offline.Payloads;
using Moq;
using System.Text.Json;
using Xunit;

namespace FProductionDashBoard.Tests.OfflineTests
{
    public class HandlerTests
    {
        // ─── ReplacementSyncHandler ───────────────────────────────────────────

        [Fact]
        public async Task ReplacementSyncHandler_DeserializesPayloadAndCallsRepo()
        {
            var repo = new Mock<IMaterialReplacementRepository>();
            repo.Setup(r => r.AddReplacementRecordAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<List<(int, int)>>()))
                .ReturnsAsync(1);

            var payload = new ReplacementPayload
            {
                EquipmentId = 10,
                EmployeeId = 20,
                Materials = new List<MaterialItem> { new() { MaterialId = 1, Quantity = 5 } }
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddReplacement,
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            var handler = new ReplacementSyncHandler(repo.Object);
            await handler.HandleAsync(op);

            repo.Verify(r => r.AddReplacementRecordAsync(10, 20, It.IsAny<string>(),
                It.Is<List<(int, int)>>(list => list.Count == 1 && list[0].Item1 == 1 && list[0].Item2 == 5)),
                Times.Once);
        }

        // ─── FirstInspectionSyncHandler ───────────────────────────────────────

        [Fact]
        public async Task FirstInspectionSyncHandler_DeserializesPayloadAndCallsRepo()
        {
            var repo = new Mock<IInspectionRecordRepository>();
            repo.Setup(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(1);

            var payload = new FirstInspectionPayload
            {
                EquipmentId = 11,
                EmployeeId = 21,
                Result = true,
                Product = "ModelA",
                ErrorCode = null,
                Description = ""
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddFirstInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            var handler = new FirstInspectionSyncHandler(repo.Object);
            await handler.HandleAsync(op);

            repo.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.First, 11, 21, true, null, "ModelA", null, ""),
                Times.Once);
        }

        // ─── RoutineInspectionSyncHandler ─────────────────────────────────────

        [Fact]
        public async Task RoutineInspectionSyncHandler_DeserializesPayloadAndCallsRepo()
        {
            var repo = new Mock<IInspectionRecordRepository>();
            repo.Setup(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(1);

            var payload = new RoutineInspectionPayload
            {
                EquipmentId = 12,
                EmployeeId = 22,
                Result = false,
                TimeSlotId = 3,
                Product = "ModelB",
                ErrorCode = "E001",
                Description = "Surface defect"
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddRoutineInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            var handler = new RoutineInspectionSyncHandler(repo.Object);
            await handler.HandleAsync(op);

            repo.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.Routine, 12, 22, false, 3, "ModelB", "E001", "Surface defect"),
                Times.Once);
        }
    }
}
