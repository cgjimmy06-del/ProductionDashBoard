using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Exceptions;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services.V1;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.DataServiceTests
{
    public class DataServiceV1Tests
    {
        private readonly Mock<IEquipmentRepository> _equipmentRep = new();
        private readonly Mock<IEmployeeRepository> _employeeRep = new();
        private readonly Mock<IMaterialRepository> _materialRep = new();
        private readonly Mock<IErrorListRepository> _errorListRep = new();
        private readonly Mock<IMaterialReplacementRepository> _materialReplacementRep = new();
        private readonly Mock<IInspectionRecordRepository> _inspectionRecordRep = new();
        private readonly Mock<ITimeSlotLookupRepository> _timeSlotLookupRep = new();
        private readonly Mock<IOfflineCacheService> _offlineCache = new();

        public DataServiceV1Tests()
        {
            // 預設連線正常
            _materialReplacementRep.Setup(r => r.CheckConnection()).Returns(true);
            _inspectionRecordRep.Setup(r => r.CheckConnection()).Returns(true);
            _offlineCache.Setup(c => c.EnqueueAsync(It.IsAny<PendingOperation>())).Returns(Task.CompletedTask);
        }

        private DataService CreateService(DateTime? businessDay = null)
        {
            var service = new DataService(
                _equipmentRep.Object,
                _employeeRep.Object,
                _materialRep.Object,
                _errorListRep.Object,
                _materialReplacementRep.Object,
                _inspectionRecordRep.Object,
                _timeSlotLookupRep.Object,
                _offlineCache.Object
            );
            service.BusinessDay = businessDay ?? DateTime.Today;
            return service;
        }

        // ─── GetAllSlotsStatusAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetAllSlotsStatusAsync_FutureSlot_ReturnsGray()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 1,
                StartAt = now.TimeOfDay.Add(TimeSpan.FromHours(1)),
                EndAt   = now.TimeOfDay.Add(TimeSpan.FromHours(2)),
                IsCrossDay = false
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(false, false)]);

            var result = await CreateService(DateTime.Today).GetAllSlotsStatusAsync([slot], 1);
            Assert.Equal(-1, result[0]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_ActiveSlotNoRecord_ReturnsYellow()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 1,
                StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)),
                EndAt   = now.TimeOfDay.Add(TimeSpan.FromHours(1)),
                IsCrossDay = false
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(false, false)]);

            var result = await CreateService(DateTime.Today).GetAllSlotsStatusAsync([slot], 1);
            Assert.Equal(1, result[0]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_EndedSlotNoRecord_ReturnsRed()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 1,
                StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)),
                EndAt   = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)),
                IsCrossDay = false
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(false, false)]);

            var result = await CreateService(DateTime.Today).GetAllSlotsStatusAsync([slot], 1);
            Assert.Equal(2, result[0]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_SlotWithPassRecord_ReturnsGreen()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 1,
                StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)),
                EndAt   = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)),
                IsCrossDay = false
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(true, true)]);

            var result = await CreateService(DateTime.Today).GetAllSlotsStatusAsync([slot], 1);
            Assert.Equal(0, result[0]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_SlotWithFailRecord_ReturnsRed()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 1,
                StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)),
                EndAt   = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)),
                IsCrossDay = false
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(true, false)]);

            var result = await CreateService(DateTime.Today).GetAllSlotsStatusAsync([slot], 1);
            Assert.Equal(2, result[0]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_MultipleSlots_ReturnsCorrectStatuses()
        {
            var now = DateTime.Now;
            var slots = new List<TimeSlotLookup>
            {
                new() { TimeSlotId = 1, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)), EndAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(2)), IsCrossDay = false },
                new() { TimeSlotId = 2, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)), EndAt = now.TimeOfDay.Add(TimeSpan.FromHours(1)), IsCrossDay = false },
                new() { TimeSlotId = 3, StartAt = now.TimeOfDay.Add(TimeSpan.FromHours(2)),      EndAt = now.TimeOfDay.Add(TimeSpan.FromHours(3)), IsCrossDay = false },
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(false, false), (false, false), (false, false)]);

            var result = await CreateService(DateTime.Today).GetAllSlotsStatusAsync(slots, 1);
            Assert.Equal(2,  result[0]);
            Assert.Equal(1,  result[1]);
            Assert.Equal(-1, result[2]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_CrossDaySlot_EndAtShiftedByOneDay()
        {
            var businessDay = DateTime.Today.AddDays(1);
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 1,
                StartAt    = new TimeSpan(23, 0, 0),
                EndAt      = new TimeSpan(1, 0, 0),
                IsCrossDay = true
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(false, false)]);

            var result = await CreateService(businessDay).GetAllSlotsStatusAsync([slot], 1);
            Assert.Equal(-1, result[0]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_EmptySlotList_ReturnsEmptyResult()
        {
            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([]);

            var result = await CreateService(DateTime.Today).GetAllSlotsStatusAsync([], 1);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_CrossDayBothShifted_ReturnsGray()
        {
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 1,
                StartAt    = new TimeSpan(1, 0, 0),
                EndAt      = new TimeSpan(2, 0, 0),
                IsCrossDay = true
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(false, false)]);

            var result = await CreateService(DateTime.Today).GetAllSlotsStatusAsync([slot], 1);
            Assert.Equal(-1, result[0]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_ActiveSlotWithPassRecord_ReturnsGreen()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 1,
                StartAt    = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)),
                EndAt      = now.TimeOfDay.Add(TimeSpan.FromHours(1)),
                IsCrossDay = false
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(true, true)]);

            var result = await CreateService(DateTime.Today).GetAllSlotsStatusAsync([slot], 1);
            Assert.Equal(0, result[0]);
        }

        // ─── AddRoutineInspectionAsync ─────────────────────────────────────────

        [Fact]
        public async Task AddRoutineInspectionAsync_WhenSlotEmpty_AddsRecord()
        {
            _inspectionRecordRep
                .Setup(r => r.ExistsInspectionInSlotAsync(1, 2, It.IsAny<DateTime>()))
                .ReturnsAsync(false);
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.Routine, 1, 10, true, 2, "PROD-A", null, null))
                .ReturnsAsync(99);

            var id = await CreateService(DateTime.Today).AddRoutineInspectionAsync(1, 10, true, 2, "PROD-A");

            Assert.Equal(99, id);
        }

        [Fact]
        public async Task AddRoutineInspectionAsync_WhenRecordExists_ThrowsBusinessRuleException()
        {
            _inspectionRecordRep
                .Setup(r => r.ExistsInspectionInSlotAsync(1, 2, It.IsAny<DateTime>()))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => CreateService(DateTime.Today).AddRoutineInspectionAsync(1, 10, true, 2, "PROD-A"));

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task AddRoutineInspectionAsync_WhenConnectionFails_EnqueuesAndThrowsOfflineException()
        {
            _inspectionRecordRep.Setup(r => r.CheckConnection()).Returns(false);

            var ex = await Assert.ThrowsAsync<OfflineOperationQueuedException>(
                () => CreateService(DateTime.Today).AddRoutineInspectionAsync(1, 10, true, 2, "PROD-A"));

            _offlineCache.Verify(c => c.EnqueueAsync(
                It.Is<PendingOperation>(op => op.OperationType == PendingOperationType.AddRoutineInspection)),
                Times.Once);
            Assert.NotEqual(Guid.Empty, ex.PendingOperationId);
        }

        // ─── AddFirstInspectionAsync ───────────────────────────────────────────

        [Fact]
        public async Task AddFirstInspectionAsync_PassResult_DelegatesToRepository()
        {
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.First, 1, 10, true, null, "PROD-A", null, null))
                .ReturnsAsync(42);

            var id = await CreateService(DateTime.Today).AddFirstInspectionAsync(1, 10, true, "PROD-A");

            Assert.Equal(42, id);
        }

        [Fact]
        public async Task AddFirstInspectionAsync_FailResultWithErrorCode_PassesAllParams()
        {
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.First, 2, 5, false, null, "PROD-B", "INSP0001", "外觀不良"))
                .ReturnsAsync(55);

            var id = await CreateService(DateTime.Today).AddFirstInspectionAsync(2, 5, false, "PROD-B", "INSP0001", "外觀不良");

            Assert.Equal(55, id);
        }

        [Fact]
        public async Task AddFirstInspectionAsync_TimeSlotIdIsAlwaysNull()
        {
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.First, It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<bool>(), null, It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(1);

            await CreateService(DateTime.Today).AddFirstInspectionAsync(1, 1, true, "P");

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.First, It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), null,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task AddFirstInspectionAsync_WhenConnectionFails_EnqueuesAndThrowsOfflineException()
        {
            _inspectionRecordRep.Setup(r => r.CheckConnection()).Returns(false);

            var ex = await Assert.ThrowsAsync<OfflineOperationQueuedException>(
                () => CreateService(DateTime.Today).AddFirstInspectionAsync(1, 10, true, "PROD-A"));

            _offlineCache.Verify(c => c.EnqueueAsync(
                It.Is<PendingOperation>(op => op.OperationType == PendingOperationType.AddFirstInspection)),
                Times.Once);
            Assert.NotEqual(Guid.Empty, ex.PendingOperationId);
        }

        // ─── AddReplacementRecordAsync ─────────────────────────────────────────

        [Fact]
        public async Task AddReplacementRecordAsync_DelegatesToRepository_WithFixedErrorCode()
        {
            var details = new List<(int materialId, int quantity)> { (1, 2), (3, 1) };
            _materialReplacementRep
                .Setup(r => r.AddReplacementRecordAsync(1, 10, "MTRP0001", details))
                .ReturnsAsync(7);

            var id = await CreateService(DateTime.Today).AddReplacementRecordAsync(1, 10, details);

            Assert.Equal(7, id);
            _materialReplacementRep.Verify(r => r.AddReplacementRecordAsync(1, 10, "MTRP0001", details), Times.Once);
        }

        [Fact]
        public async Task AddReplacementRecordAsync_EmptyMaterialList_StillDelegates()
        {
            var details = new List<(int materialId, int quantity)>();
            _materialReplacementRep
                .Setup(r => r.AddReplacementRecordAsync(2, 3, "MTRP0001", details))
                .ReturnsAsync(0);

            var id = await CreateService(DateTime.Today).AddReplacementRecordAsync(2, 3, details);

            Assert.Equal(0, id);
        }

        [Fact]
        public async Task AddReplacementRecordAsync_WhenConnectionFails_EnqueuesAndThrowsOfflineException()
        {
            _materialReplacementRep.Setup(r => r.CheckConnection()).Returns(false);
            var details = new List<(int materialId, int quantity)> { (1, 2) };

            var ex = await Assert.ThrowsAsync<OfflineOperationQueuedException>(
                () => CreateService(DateTime.Today).AddReplacementRecordAsync(1, 10, details));

            _offlineCache.Verify(c => c.EnqueueAsync(
                It.Is<PendingOperation>(op => op.OperationType == PendingOperationType.AddReplacement)),
                Times.Once);
            Assert.NotEqual(Guid.Empty, ex.PendingOperationId);
        }

        // ─── CheckAndInsertMissedInspectionAsync ───────────────────────────────

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_EndedSlotNoRecord_InsertsOnce()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 5,
                StartAt    = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)),
                EndAt      = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)),
                IsCrossDay = false
            };

            _inspectionRecordRep
                .Setup(r => r.ExistsInspectionInSlotAsync(1, 5, It.IsAny<DateTime>()))
                .ReturnsAsync(false);
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.Routine, 1, 1, false, 5, null, "RTIN0001", null))
                .ReturnsAsync(1);

            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync([slot], 1);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.Routine, 1, 1, false, 5, null, "RTIN0001", null), Times.Once);
        }

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_EndedSlotWithRecord_DoesNotInsert()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 5,
                StartAt    = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)),
                EndAt      = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)),
                IsCrossDay = false
            };

            _inspectionRecordRep
                .Setup(r => r.ExistsInspectionInSlotAsync(1, 5, It.IsAny<DateTime>()))
                .ReturnsAsync(true);

            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync([slot], 1);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_ActiveSlot_DoesNotInsert()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 6,
                StartAt    = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)),
                EndAt      = now.TimeOfDay.Add(TimeSpan.FromHours(1)),
                IsCrossDay = false
            };

            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync([slot], 1);

            _inspectionRecordRep.Verify(r => r.ExistsInspectionInSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_MultipleEndedSlots_InsertsForEachMissed()
        {
            var now = DateTime.Now;
            var slots = new List<TimeSlotLookup>
            {
                new() { TimeSlotId = 1, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(5)), EndAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(4)), IsCrossDay = false },
                new() { TimeSlotId = 2, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)), EndAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(2)), IsCrossDay = false },
            };

            _inspectionRecordRep.Setup(r => r.ExistsInspectionInSlotAsync(1, 1, It.IsAny<DateTime>())).ReturnsAsync(true);
            _inspectionRecordRep.Setup(r => r.ExistsInspectionInSlotAsync(1, 2, It.IsAny<DateTime>())).ReturnsAsync(false);
            _inspectionRecordRep.Setup(r => r.AddInspectionRecordAsync(
                InspectionType.Routine, 1, 1, false, 2, null, "RTIN0001", null)).ReturnsAsync(1);

            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync(slots, 1);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_EmptySlotList_DoesNothing()
        {
            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync([], 1);

            _inspectionRecordRep.Verify(r => r.ExistsInspectionInSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_FutureSlot_DoesNotInsert()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 9,
                StartAt    = now.TimeOfDay.Add(TimeSpan.FromHours(2)),
                EndAt      = now.TimeOfDay.Add(TimeSpan.FromHours(3)),
                IsCrossDay = false
            };

            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync([slot], 1);

            _inspectionRecordRep.Verify(r => r.ExistsInspectionInSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_MixedSlots_InsertsOnlyForEndedMissed()
        {
            var now = DateTime.Now;
            var slots = new List<TimeSlotLookup>
            {
                new() { TimeSlotId = 1, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(5)), EndAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(4)), IsCrossDay = false },
                new() { TimeSlotId = 2, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)), EndAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(2)), IsCrossDay = false },
                new() { TimeSlotId = 3, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)), EndAt = now.TimeOfDay.Add(TimeSpan.FromHours(1)),      IsCrossDay = false },
                new() { TimeSlotId = 4, StartAt = now.TimeOfDay.Add(TimeSpan.FromHours(2)),      EndAt = now.TimeOfDay.Add(TimeSpan.FromHours(3)),      IsCrossDay = false },
            };

            _inspectionRecordRep.Setup(r => r.ExistsInspectionInSlotAsync(1, 1, It.IsAny<DateTime>())).ReturnsAsync(true);
            _inspectionRecordRep.Setup(r => r.ExistsInspectionInSlotAsync(1, 2, It.IsAny<DateTime>())).ReturnsAsync(false);
            _inspectionRecordRep.Setup(r => r.AddInspectionRecordAsync(
                InspectionType.Routine, 1, 1, false, 2, null, "RTIN0001", null)).ReturnsAsync(1);

            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync(slots, 1);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        }

        // ─── GetCurrentTimeSlotIdAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetCurrentTimeSlotIdAsync_DelegatesToRepository()
        {
            var businessDay = DateTime.Today;
            _timeSlotLookupRep
                .Setup(r => r.GetCurrentTimeSlotIdAsync(businessDay))
                .ReturnsAsync(3);

            var result = await CreateService(businessDay).GetCurrentTimeSlotIdAsync();

            Assert.Equal(3, result);
            _timeSlotLookupRep.Verify(r => r.GetCurrentTimeSlotIdAsync(businessDay), Times.Once);
        }

        [Fact]
        public async Task GetCurrentTimeSlotIdAsync_WhenNoActiveSlot_ReturnsNull()
        {
            _timeSlotLookupRep
                .Setup(r => r.GetCurrentTimeSlotIdAsync(It.IsAny<DateTime>()))
                .ReturnsAsync((int?)null);

            var result = await CreateService(DateTime.Today).GetCurrentTimeSlotIdAsync();

            Assert.Null(result);
        }
    }
}
