using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Exceptions;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services.V1;
using Microsoft.EntityFrameworkCore;
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
        private readonly Mock<IRolePermissionRepository> _rolePermissionRep = new();
        private readonly Mock<ITuningRecordRepository> _tuningRecordRep = new();
        private readonly Mock<IProductPartRepository> _productPartRep = new();
        private readonly Mock<IProductRepository> _productRep = new();
        private readonly Mock<ISopChecklistRepository> _sopChecklistRep = new();
        private readonly Mock<IEquipmentProductRepository> _equipmentProductRep = new();
        private readonly Mock<IOrderProductionRepository> _orderProductionRep = new();
        private readonly Mock<IDbContextFactory<MesDbContext>> _mesFactory = new();
        public DataServiceV1Tests()
        {
            // 預設連線正常
            _materialReplacementRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _inspectionRecordRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _timeSlotLookupRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _rolePermissionRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _materialRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _productPartRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _productRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _sopChecklistRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
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
                _offlineCache.Object,
                _rolePermissionRep.Object,
                _tuningRecordRep.Object,
                _productPartRep.Object,
                _productRep.Object,
                _sopChecklistRep.Object,
                _equipmentProductRep.Object,
                _orderProductionRep.Object,
                _mesFactory.Object
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
                    InspectionType.Routine, 1, 10, true, 2, null, null, null, It.IsAny<DateTime?>()))
                .ReturnsAsync(99);

            var id = await CreateService(DateTime.Today).AddRoutineInspectionAsync(1, 10, true, 2, null);

            Assert.Equal(99, id);
        }

        [Fact]
        public async Task AddRoutineInspectionAsync_WhenRecordExists_ThrowsBusinessRuleException()
        {
            _inspectionRecordRep
                .Setup(r => r.ExistsInspectionInSlotAsync(1, 2, It.IsAny<DateTime>()))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => CreateService(DateTime.Today).AddRoutineInspectionAsync(1, 10, true, 2, null));

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()), Times.Never);
        }

        [Fact]
        public async Task AddRoutineInspectionAsync_WhenConnectionFails_EnqueuesAndThrowsOfflineException()
        {
            _inspectionRecordRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);

            var ex = await Assert.ThrowsAsync<OfflineOperationQueuedException>(
                () => CreateService(DateTime.Today).AddRoutineInspectionAsync(1, 10, true, 2, null));

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
                    InspectionType.First, 1, 10, true, null, null, null, null, It.IsAny<DateTime?>()))
                .ReturnsAsync(42);

            var id = await CreateService(DateTime.Today).AddFirstInspectionAsync(1, 10, true, null);

            Assert.Equal(42, id);
        }

        [Fact]
        public async Task AddFirstInspectionAsync_FailResultWithErrorCode_PassesAllParams()
        {
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.First, 2, 5, false, null, 1, "INSP0001", "外觀不良", It.IsAny<DateTime?>()))
                .ReturnsAsync(55);

            var id = await CreateService(DateTime.Today).AddFirstInspectionAsync(2, 5, false, 1, "INSP0001", "外觀不良");

            Assert.Equal(55, id);
        }

        [Fact]
        public async Task AddFirstInspectionAsync_TimeSlotIdIsAlwaysNull()
        {
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.First, It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<bool>(), null, It.IsAny<int?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(1);

            await CreateService(DateTime.Today).AddFirstInspectionAsync(1, 1, true, null);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.First, It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), null,
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()), Times.Once);
        }

        [Fact]
        public async Task AddFirstInspectionAsync_WhenConnectionFails_EnqueuesAndThrowsOfflineException()
        {
            _inspectionRecordRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);

            var ex = await Assert.ThrowsAsync<OfflineOperationQueuedException>(
                () => CreateService(DateTime.Today).AddFirstInspectionAsync(1, 10, true, null));

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
                .Setup(r => r.AddReplacementRecordAsync(1, 10, "MTRP0001", details, It.IsAny<DateTime?>()))
                .ReturnsAsync(7);

            var id = await CreateService(DateTime.Today).AddReplacementRecordAsync(1, 10, details);

            Assert.Equal(7, id);
            _materialReplacementRep.Verify(r => r.AddReplacementRecordAsync(1, 10, "MTRP0001", details, It.IsAny<DateTime?>()), Times.Once);
        }

        [Fact]
        public async Task AddReplacementRecordAsync_EmptyMaterialList_StillDelegates()
        {
            var details = new List<(int materialId, int quantity)>();
            _materialReplacementRep
                .Setup(r => r.AddReplacementRecordAsync(2, 3, "MTRP0001", details, It.IsAny<DateTime?>()))
                .ReturnsAsync(0);

            var id = await CreateService(DateTime.Today).AddReplacementRecordAsync(2, 3, details);

            Assert.Equal(0, id);
        }

        [Fact]
        public async Task AddReplacementRecordAsync_WhenConnectionFails_EnqueuesAndThrowsOfflineException()
        {
            _materialReplacementRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
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
                    InspectionType.Routine, 1, 1, false, 5, null, "RTIN0001", null, It.IsAny<DateTime?>()))
                .ReturnsAsync(1);

            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync([slot], 1);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.Routine, 1, 1, false, 5, null, "RTIN0001", null, It.IsAny<DateTime?>()), Times.Once);
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
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()), Times.Never);
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
                InspectionType.Routine, 1, 1, false, 2, null, "RTIN0001", null, It.IsAny<DateTime?>())).ReturnsAsync(1);

            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync(slots, 1);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()), Times.Once);
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
                InspectionType.Routine, 1, 1, false, 2, null, "RTIN0001", null, It.IsAny<DateTime?>())).ReturnsAsync(1);

            await CreateService(DateTime.Today).CheckAndInsertMissedInspectionAsync(slots, 1);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()), Times.Once);
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

        // ─── GetAllPermissionsAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetAllPermissionsAsync_WhenConnected_ReturnsPermissionsFromRepository()
        {
            var perms = new List<Permission> { new() { PermissionId = 1, Name = "View" } };
            _rolePermissionRep.Setup(r => r.GetAllPermissionsAsync()).ReturnsAsync(perms);

            var result = await CreateService().GetAllPermissionsAsync();

            Assert.Equal(perms, result);
        }

        [Fact]
        public async Task GetAllPermissionsAsync_WhenConnectionFails_Throws()
        {
            _rolePermissionRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().GetAllPermissionsAsync());
        }

        // ─── GetAllRolesAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllRolesAsync_WhenConnected_ReturnsRolesFromRepository()
        {
            var roles = new List<Role> { new() { RoleId = 1, Name = "Admin" } };
            _rolePermissionRep.Setup(r => r.GetAllRolesAsync()).ReturnsAsync(roles);

            var result = await CreateService().GetAllRolesAsync();

            Assert.Equal(roles, result);
        }

        [Fact]
        public async Task GetAllRolesAsync_WhenConnectionFails_Throws()
        {
            _rolePermissionRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().GetAllRolesAsync());
        }

        // ─── AddRoleAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task AddRoleAsync_WhenConnected_DelegatesToRepository()
        {
            var dto = new RoleFormDto { RoleId = 5, Name = "Operator", SelectedPermissionIds = [1, 2] };
            _rolePermissionRep
                .Setup(r => r.AddRoleWithPermissionsAsync(dto.RoleId, dto.Name, dto.Description, dto.SelectedPermissionIds))
                .Returns(Task.CompletedTask);

            await CreateService().AddRoleAsync(dto);

            _rolePermissionRep.Verify(r => r.AddRoleWithPermissionsAsync(
                dto.RoleId, dto.Name, dto.Description, dto.SelectedPermissionIds), Times.Once);
        }

        [Fact]
        public async Task AddRoleAsync_WhenConnectionFails_Throws()
        {
            _rolePermissionRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().AddRoleAsync(new RoleFormDto { RoleId = 1, Name = "Test" }));
        }

        // ─── UpdateRoleAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateRoleAsync_WhenConnected_DelegatesToRepository()
        {
            var dto = new RoleFormDto { Id = 3, RoleId = 3, Name = "Updated", SelectedPermissionIds = [2] };
            _rolePermissionRep
                .Setup(r => r.UpdateRoleWithPermissionsAsync(dto.Id!.Value, dto.Name, dto.Description, dto.SelectedPermissionIds))
                .Returns(Task.CompletedTask);

            await CreateService().UpdateRoleAsync(dto);

            _rolePermissionRep.Verify(r => r.UpdateRoleWithPermissionsAsync(
                dto.Id!.Value, dto.Name, dto.Description, dto.SelectedPermissionIds), Times.Once);
        }

        [Fact]
        public async Task UpdateRoleAsync_WhenConnectionFails_Throws()
        {
            _rolePermissionRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().UpdateRoleAsync(new RoleFormDto { Id = 1, RoleId = 1, Name = "X" }));
        }

        // ─── DeleteRoleAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteRoleAsync_WhenNoEmployees_DeletesRole()
        {
            _rolePermissionRep.Setup(r => r.HasEmployeesByRoleAsync(7)).ReturnsAsync(false);
            _rolePermissionRep.Setup(r => r.DeleteAsync(7)).Returns(Task.CompletedTask);

            await CreateService().DeleteRoleAsync(7);

            _rolePermissionRep.Verify(r => r.DeleteAsync(7), Times.Once);
        }

        [Fact]
        public async Task DeleteRoleAsync_WhenRoleHasEmployees_Throws()
        {
            _rolePermissionRep.Setup(r => r.HasEmployeesByRoleAsync(7)).ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().DeleteRoleAsync(7));

            _rolePermissionRep.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteRoleAsync_WhenConnectionFails_Throws()
        {
            _rolePermissionRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().DeleteRoleAsync(1));
        }

        // ─── ProductPart CRUD ────────────────────────────────────────────────

        [Fact]
        public async Task GetAllProductPartsAsync_WhenConnectionFails_Throws()
        {
            _productPartRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().GetAllProductPartsAsync());
        }

        [Fact]
        public async Task GetAllProductPartsAsync_ReturnsRepoList()
        {
            _productPartRep.Setup(r => r.GetAllAsync())
                .ReturnsAsync([
                    new ProductPart { PartId = 1, PartNo = "ABC11111" },
                    new ProductPart { PartId = 2, PartNo = "ABC22222" }
                ]);

            var result = await CreateService().GetAllProductPartsAsync();
            Assert.Equal(2, result.Count);
            Assert.Equal("ABC11111", result[0].PartNo);
        }

        [Fact]
        public async Task AddProductPartAsync_WhenConnectionFails_Throws()
        {
            _productPartRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().AddProductPartAsync(new ProductPartFormDto { PartNo = "TEST0001" }));
        }

        [Fact]
        public async Task AddProductPartAsync_ReturnsBackfilledPartId()
        {
            _productPartRep.Setup(r => r.AddAsync(It.IsAny<ProductPart>()))
                .Callback<ProductPart>(p => p.PartId = 42)
                .Returns(Task.CompletedTask);

            var newId = await CreateService().AddProductPartAsync(
                new ProductPartFormDto { PartNo = "TEST0001", Brand = "B", Name = "N" });
            Assert.Equal(42, newId);
        }

        // ─── SOP lookup ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetMaterialsByTypeAsync_WhenConnectionFails_Throws()
        {
            _materialRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().GetMaterialsByTypeAsync(1));
        }

        [Fact]
        public async Task GetMaterialsByTypeAsync_FiltersByTypeId()
        {
            _materialRep.Setup(r => r.GetAllAsync())
                .ReturnsAsync([
                    new Material { MaterialId = 1, TypeId = 1, Name = "Station-A" },
                    new Material { MaterialId = 2, TypeId = 1, Name = "Station-B" },
                    new Material { MaterialId = 3, TypeId = 2, Name = "Fixture-A" },
                    new Material { MaterialId = 4, TypeId = null, Name = "Untyped" }
                ]);

            var stations = await CreateService().GetMaterialsByTypeAsync(1);
            Assert.Equal(2, stations.Count);
            Assert.All(stations, m => Assert.Equal(1, m.TypeId));

            var fixtures = await CreateService().GetMaterialsByTypeAsync(2);
            Assert.Single(fixtures);
            Assert.Equal(3, fixtures[0].MaterialId);
        }

        [Fact]
        public async Task GetProductModelsAsync_ReturnsRepoList()
        {
            _productRep.Setup(r => r.GetModelsAsync())
                .ReturnsAsync([new ProductModel { ModelId = 100, Name = "100A" }]);

            var result = await CreateService().GetProductModelsAsync();
            Assert.Single(result);
            Assert.Equal("100A", result[0].Name);
        }

        [Fact]
        public async Task GetWorkProcessesAsync_ReturnsRepoList()
        {
            _sopChecklistRep.Setup(r => r.GetProcessesAsync())
                .ReturnsAsync([new WorkProcess { ProcessId = 1, Name = "加工" }]);

            var result = await CreateService().GetWorkProcessesAsync();
            Assert.Single(result);
            Assert.Equal("加工", result[0].Name);
        }

        // ─── SopChecklist CRUD ───────────────────────────────────────────────

        [Fact]
        public async Task GetSopChecklistWithItemsAsync_WhenConnectionFails_Throws()
        {
            _sopChecklistRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().GetSopChecklistWithItemsAsync(1));
        }

        [Fact]
        public async Task GetSopChecklistWithItemsAsync_DelegatesToRepository()
        {
            var sop = new SopChecklist { SopId = 7, Remark = "test" };
            _sopChecklistRep.Setup(r => r.GetWithItemsAsync(7)).ReturnsAsync(sop);

            var result = await CreateService().GetSopChecklistWithItemsAsync(7);
            Assert.Same(sop, result);
        }

        [Fact]
        public async Task DeleteSopChecklistAsync_WhenConnectionFails_Throws()
        {
            _sopChecklistRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().DeleteSopChecklistAsync(1));
        }

        [Fact]
        public async Task DeleteSopChecklistAsync_CallsRepositoryDelete()
        {
            _sopChecklistRep.Setup(r => r.DeleteAsync(5)).Returns(Task.CompletedTask).Verifiable();
            await CreateService().DeleteSopChecklistAsync(5);
            _sopChecklistRep.Verify();
        }

        [Fact]
        public async Task AddSopChecklistAsync_WhenConnectionFails_Throws()
        {
            _sopChecklistRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().AddSopChecklistAsync(new SopChecklistFormDto()));
        }

        [Fact]
        public async Task UpdateSopChecklistAsync_WhenIdIsNull_Throws()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().UpdateSopChecklistAsync(new SopChecklistFormDto { Id = null }));
        }

        // 註：AddSopChecklistAsync / UpdateSopChecklistAsync 的 happy path 使用單一 DbContext
        // 進行 EnsureProduct + Add/Update Items，需要 EF 真實 ctx 或 in-memory provider 驗證；
        // 由 PR-A 手動 UI 驗證涵蓋。
    }
}
