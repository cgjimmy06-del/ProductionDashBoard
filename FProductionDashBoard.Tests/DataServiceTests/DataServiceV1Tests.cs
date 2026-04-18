using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
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

        private DataService CreateService(DateTime? businessDay = null)
        {
            var service = new DataService(
                _equipmentRep.Object,
                _employeeRep.Object,
                _materialRep.Object,
                _errorListRep.Object,
                _materialReplacementRep.Object,
                _inspectionRecordRep.Object,
                _timeSlotLookupRep.Object
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

            var service = CreateService(DateTime.Today);
            var result = await service.GetAllSlotsStatusAsync([slot], 1);

            Assert.Equal(-1, result[0]); // 灰：尚未開始
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

            var service = CreateService(DateTime.Today);
            var result = await service.GetAllSlotsStatusAsync([slot], 1);

            Assert.Equal(1, result[0]); // 黃：進行中但未巡檢
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

            var service = CreateService(DateTime.Today);
            var result = await service.GetAllSlotsStatusAsync([slot], 1);

            Assert.Equal(2, result[0]); // 紅：時段已過且未巡檢
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

            var service = CreateService(DateTime.Today);
            var result = await service.GetAllSlotsStatusAsync([slot], 1);

            Assert.Equal(0, result[0]); // 綠：有紀錄且合格
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

            var service = CreateService(DateTime.Today);
            var result = await service.GetAllSlotsStatusAsync([slot], 1);

            Assert.Equal(2, result[0]); // 紅：有紀錄但不合格
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_MultipleSlots_ReturnsCorrectStatuses()
        {
            var now = DateTime.Now;
            var slots = new List<TimeSlotLookup>
            {
                new() { TimeSlotId = 1, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)), EndAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(2)), IsCrossDay = false }, // 過去, 無紀錄 → 紅
                new() { TimeSlotId = 2, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)), EndAt = now.TimeOfDay.Add(TimeSpan.FromHours(1)), IsCrossDay = false },      // 進行中, 無紀錄 → 黃
                new() { TimeSlotId = 3, StartAt = now.TimeOfDay.Add(TimeSpan.FromHours(2)),      EndAt = now.TimeOfDay.Add(TimeSpan.FromHours(3)), IsCrossDay = false },       // 未來 → 灰
            };

            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([(false, false), (false, false), (false, false)]);

            var service = CreateService(DateTime.Today);
            var result = await service.GetAllSlotsStatusAsync(slots, 1);

            Assert.Equal(2,  result[0]);
            Assert.Equal(1,  result[1]);
            Assert.Equal(-1, result[2]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_CrossDaySlot_EndAtShiftedByOneDay()
        {
            // IsCrossDay=true, EndAt(01:00) < StartAt(23:00)：只有 slotEnd +1 天
            // businessDay = 明天 → slotStart = 明天23:00、slotEnd = 後天01:00，永遠在未來 → 灰
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

            var service = CreateService(businessDay);
            var result = await service.GetAllSlotsStatusAsync([slot], 1);

            Assert.Equal(-1, result[0]); // 灰：跨日時段起始映射到明天23:00，尚未開始
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

            var service = CreateService(DateTime.Today);
            var id = await service.AddRoutineInspectionAsync(1, 10, true, 2, "PROD-A");

            Assert.Equal(99, id);
            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.Routine, 1, 10, true, 2, "PROD-A", null, null), Times.Once);
        }

        [Fact]
        public async Task AddRoutineInspectionAsync_WhenRecordExists_ThrowsInvalidOperationException()
        {
            _inspectionRecordRep
                .Setup(r => r.ExistsInspectionInSlotAsync(1, 2, It.IsAny<DateTime>()))
                .ReturnsAsync(true);

            var service = CreateService(DateTime.Today);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddRoutineInspectionAsync(1, 10, true, 2, "PROD-A"));

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
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

            var service = CreateService(DateTime.Today);
            await service.CheckAndInsertMissedInspectionAsync([slot], 1);

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

            var service = CreateService(DateTime.Today);
            await service.CheckAndInsertMissedInspectionAsync([slot], 1);

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
                EndAt      = now.TimeOfDay.Add(TimeSpan.FromHours(1)), // 尚未結束
                IsCrossDay = false
            };

            var service = CreateService(DateTime.Today);
            await service.CheckAndInsertMissedInspectionAsync([slot], 1);

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

            var service = CreateService(DateTime.Today);
            await service.CheckAndInsertMissedInspectionAsync(slots, 1);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()), Times.Once); // 只有 slot 2 需要補
        }

        // ─── GetCurrentTimeSlotIdAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetCurrentTimeSlotIdAsync_DelegatesToRepository()
        {
            var businessDay = DateTime.Today;
            _timeSlotLookupRep
                .Setup(r => r.GetCurrentTimeSlotIdAsync(businessDay))
                .ReturnsAsync(3);

            var service = CreateService(businessDay);
            var result = await service.GetCurrentTimeSlotIdAsync();

            Assert.Equal(3, result);
            _timeSlotLookupRep.Verify(r => r.GetCurrentTimeSlotIdAsync(businessDay), Times.Once);
        }

        [Fact]
        public async Task GetCurrentTimeSlotIdAsync_WhenNoActiveSlot_ReturnsNull()
        {
            _timeSlotLookupRep
                .Setup(r => r.GetCurrentTimeSlotIdAsync(It.IsAny<DateTime>()))
                .ReturnsAsync((int?)null);

            var service = CreateService(DateTime.Today);
            var result = await service.GetCurrentTimeSlotIdAsync();

            Assert.Null(result);
        }

        // ─── AddFirstInspectionAsync ───────────────────────────────────────────

        [Fact]
        public async Task AddFirstInspectionAsync_PassResult_DelegatesToRepository()
        {
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.First, 1, 10, true, null, "PROD-A", null, null))
                .ReturnsAsync(42);

            var service = CreateService(DateTime.Today);
            var id = await service.AddFirstInspectionAsync(1, 10, true, "PROD-A");

            Assert.Equal(42, id);
            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.First, 1, 10, true, null, "PROD-A", null, null), Times.Once);
        }

        [Fact]
        public async Task AddFirstInspectionAsync_FailResultWithErrorCode_PassesAllParams()
        {
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.First, 2, 5, false, null, "PROD-B", "INSP0001", "外觀不良"))
                .ReturnsAsync(55);

            var service = CreateService(DateTime.Today);
            var id = await service.AddFirstInspectionAsync(2, 5, false, "PROD-B", "INSP0001", "外觀不良");

            Assert.Equal(55, id);
            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.First, 2, 5, false, null, "PROD-B", "INSP0001", "外觀不良"), Times.Once);
        }

        [Fact]
        public async Task AddFirstInspectionAsync_TimeSlotIdIsAlwaysNull()
        {
            // 首件不帶 timeSlotId，固定傳 null
            _inspectionRecordRep
                .Setup(r => r.AddInspectionRecordAsync(
                    InspectionType.First, It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<bool>(), null, It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(1);

            var service = CreateService(DateTime.Today);
            await service.AddFirstInspectionAsync(1, 1, true, "P");

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                InspectionType.First, It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), null,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        }

        // ─── AddReplacementRecordAsync ─────────────────────────────────────────

        [Fact]
        public async Task AddReplacementRecordAsync_DelegatesToRepository_WithFixedErrorCode()
        {
            var details = new List<(int materialId, int quantity)> { (1, 2), (3, 1) };
            _materialReplacementRep
                .Setup(r => r.AddReplacementRecordAsync(1, 10, "MTRP0001", details))
                .ReturnsAsync(7);

            var service = CreateService(DateTime.Today);
            var id = await service.AddReplacementRecordAsync(1, 10, details);

            Assert.Equal(7, id);
            _materialReplacementRep.Verify(
                r => r.AddReplacementRecordAsync(1, 10, "MTRP0001", details), Times.Once);
        }

        [Fact]
        public async Task AddReplacementRecordAsync_EmptyMaterialList_StillDelegates()
        {
            var details = new List<(int materialId, int quantity)>();
            _materialReplacementRep
                .Setup(r => r.AddReplacementRecordAsync(2, 3, "MTRP0001", details))
                .ReturnsAsync(0);

            var service = CreateService(DateTime.Today);
            var id = await service.AddReplacementRecordAsync(2, 3, details);

            Assert.Equal(0, id);
        }

        // ─── GetAllSlotsStatusAsync 補充 edge cases ────────────────────────────

        [Fact]
        public async Task GetAllSlotsStatusAsync_EmptySlotList_ReturnsEmptyResult()
        {
            _inspectionRecordRep
                .Setup(r => r.GetStatusForAllSlotsAsync(1, It.IsAny<DateTime>()))
                .ReturnsAsync([]);

            var service = CreateService(DateTime.Today);
            var result = await service.GetAllSlotsStatusAsync([], 1);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_CrossDayBothShifted_ReturnsGray()
        {
            // IsCrossDay=true, EndAt(02:00) > StartAt(01:00)：slotStart 和 slotEnd 都 +1 天
            // businessDay = today → slotStart = 明天 01:00、slotEnd = 明天 02:00 → 尚未開始 → 灰
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

            var service = CreateService(DateTime.Today);
            var result = await service.GetAllSlotsStatusAsync([slot], 1);

            Assert.Equal(-1, result[0]);
        }

        [Fact]
        public async Task GetAllSlotsStatusAsync_ActiveSlotWithPassRecord_ReturnsGreen()
        {
            // 時段進行中，且已有合格紀錄 → 綠 (有紀錄優先於時段狀態)
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

            var service = CreateService(DateTime.Today);
            var result = await service.GetAllSlotsStatusAsync([slot], 1);

            Assert.Equal(0, result[0]); // 綠：進行中且合格
        }

        // ─── CheckAndInsertMissedInspectionAsync 補充 edge cases ──────────────

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_EmptySlotList_DoesNothing()
        {
            var service = CreateService(DateTime.Today);
            await service.CheckAndInsertMissedInspectionAsync([], 1);

            _inspectionRecordRep.Verify(r => r.ExistsInspectionInSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_FutureSlot_DoesNotInsert()
        {
            var now = DateTime.Now;
            var slot = new TimeSlotLookup
            {
                TimeSlotId = 9,
                StartAt    = now.TimeOfDay.Add(TimeSpan.FromHours(2)), // 尚未開始
                EndAt      = now.TimeOfDay.Add(TimeSpan.FromHours(3)),
                IsCrossDay = false
            };

            var service = CreateService(DateTime.Today);
            await service.CheckAndInsertMissedInspectionAsync([slot], 1);

            _inspectionRecordRep.Verify(r => r.ExistsInspectionInSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task CheckAndInsertMissedInspectionAsync_MixedSlots_InsertsOnlyForEndedMissed()
        {
            var now = DateTime.Now;
            var slots = new List<TimeSlotLookup>
            {
                new() { TimeSlotId = 1, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(5)), EndAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(4)), IsCrossDay = false }, // 結束, 有紀錄 → 跳過
                new() { TimeSlotId = 2, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(3)), EndAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(2)), IsCrossDay = false }, // 結束, 無紀錄 → 補
                new() { TimeSlotId = 3, StartAt = now.TimeOfDay.Subtract(TimeSpan.FromHours(1)), EndAt = now.TimeOfDay.Add(TimeSpan.FromHours(1)),      IsCrossDay = false }, // 進行中 → 跳過
                new() { TimeSlotId = 4, StartAt = now.TimeOfDay.Add(TimeSpan.FromHours(2)),      EndAt = now.TimeOfDay.Add(TimeSpan.FromHours(3)),      IsCrossDay = false }, // 未來 → 跳過
            };

            _inspectionRecordRep.Setup(r => r.ExistsInspectionInSlotAsync(1, 1, It.IsAny<DateTime>())).ReturnsAsync(true);
            _inspectionRecordRep.Setup(r => r.ExistsInspectionInSlotAsync(1, 2, It.IsAny<DateTime>())).ReturnsAsync(false);
            _inspectionRecordRep.Setup(r => r.AddInspectionRecordAsync(
                InspectionType.Routine, 1, 1, false, 2, null, "RTIN0001", null)).ReturnsAsync(1);

            var service = CreateService(DateTime.Today);
            await service.CheckAndInsertMissedInspectionAsync(slots, 1);

            _inspectionRecordRep.Verify(r => r.AddInspectionRecordAsync(
                It.IsAny<InspectionType>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        }
    }
}
