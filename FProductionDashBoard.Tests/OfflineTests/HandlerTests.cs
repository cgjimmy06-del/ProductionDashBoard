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
                It.IsAny<List<(int, int)>>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(1);

            var operatedAt = new DateTime(2026, 4, 20, 10, 0, 0);
            var payload = new ReplacementPayload
            {
                EquipmentId = 10,
                EmployeeId = 20,
                Materials = new List<MaterialItem> { new() { MaterialId = 1, Quantity = 5 } },
                OperatedAt = operatedAt
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddReplacement,
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            var handler = new ReplacementSyncHandler(repo.Object);
            await handler.HandleAsync(op);

            repo.Verify(r => r.AddReplacementRecordAsync(10, 20, It.IsAny<string>(),
                It.Is<List<(int, int)>>(list => list.Count == 1 && list[0].Item1 == 1 && list[0].Item2 == 5),
                operatedAt),
                Times.Once);
        }

        // ─── FirstInspectionSyncHandler ───────────────────────────────────────

        [Fact]
        public async Task FirstInspectionSyncHandler_DeserializesPayloadAndCallsRepo()
        {
            var repo = new Mock<IInspectionRecordRepository>();
            repo.Setup(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(1);

            var operatedAt = new DateTime(2026, 4, 20, 10, 0, 0);
            var payload = new FirstInspectionPayload
            {
                EquipmentId = 11, EmployeeId = 21,
                Result = true, ProductId = 1,
                ErrorCode = null, Description = "",
                OperatedAt = operatedAt
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddFirstInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            var handler = new FirstInspectionSyncHandler(repo.Object);
            await handler.HandleAsync(op);

            repo.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.First, 11, 21, true, null, null, null, "",
                operatedAt),
                Times.Once);
        }

        // ─── RoutineInspectionSyncHandler ─────────────────────────────────────

        [Fact]
        public async Task RoutineInspectionSyncHandler_DeserializesPayloadAndCallsRepo()
        {
            var repo = new Mock<IInspectionRecordRepository>();
            repo.Setup(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(1);

            var operatedAt = new DateTime(2026, 4, 20, 14, 30, 0);
            var payload = new RoutineInspectionPayload
            {
                EquipmentId = 12, EmployeeId = 22,
                Result = false, TimeSlotId = 3,
                ProductId = 1, ErrorCode = "E001", Description = "Surface defect",
                OperatedAt = operatedAt
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddRoutineInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            var handler = new RoutineInspectionSyncHandler(repo.Object);
            await handler.HandleAsync(op);

            repo.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.Routine, 12, 22, false, 3, 1, "E001", "Surface defect",
                operatedAt),
                Times.Once);
        }
    }
}
