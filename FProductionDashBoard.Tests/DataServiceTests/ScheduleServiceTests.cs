using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Repositories.ExtraDb;
using FProductionDashBoard.Services.Exceptions;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services.V1;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.DataServiceTests
{
    public class ScheduleServiceTests
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
        private readonly Mock<IProductPartRepository> _productPartRep = new();
        private readonly Mock<IProductRepository> _productRep = new();
        private readonly Mock<ISopChecklistRepository> _sopChecklistRep = new();
        private readonly Mock<IEquipmentProductRepository> _equipmentProductRep = new();
        private readonly Mock<IOrderProductionRepository> _orderProductionRep = new();
        private readonly Mock<IProgramTuningRecordRepository> _programTuningRep = new();
        private readonly Mock<IScheduleRepository> _scheduleRep = new();
        private readonly Mock<IDbContextFactory<MesDbContext>> _mesFactory = new();
        private readonly Mock<IInfoDbRepository> _infoRep = new();
        private readonly Mock<IDataDbRepository> _dataRep = new();

        private DataService CreateService()
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
                _productPartRep.Object,
                _productRep.Object,
                _sopChecklistRep.Object,
                _equipmentProductRep.Object,
                _orderProductionRep.Object,
                _programTuningRep.Object,
                _scheduleRep.Object,
                _mesFactory.Object,
                _infoRep.Object,
                _dataRep.Object
            );
            service.BusinessDay = DateTime.Today;
            return service;
        }

        // ─── AddScheduleAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task AddScheduleAsync_WhenConnectionFails_Throws()
        {
            _scheduleRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddScheduleAsync(new ScheduleCreateDto { ProductId = 1, ProcessId = 1, Quantity = 10, ReceivedBy = 1 }));
        }

        [Fact]
        public async Task AddScheduleAsync_WhenConnected_DelegatesToRepository()
        {
            _scheduleRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _scheduleRep.Setup(r => r.AddAsync(It.IsAny<Schedule>())).ReturnsAsync(42);
            var service = CreateService();

            var result = await service.AddScheduleAsync(new ScheduleCreateDto { ProductId = 1, ProcessId = 1, Quantity = 10, ReceivedBy = 1 });

            Assert.Equal(42, result);
        }

        // ─── MarkScheduledAsync ───────────────────────────────────────────────

        [Fact]
        public async Task MarkScheduledAsync_WhenConnectionFails_Throws()
        {
            _scheduleRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.MarkScheduledAsync(1, 1));
        }

        // ─── MarkReleasedAsync ────────────────────────────────────────────────

        [Fact]
        public async Task MarkReleasedAsync_WhenConnectionFails_Throws()
        {
            _scheduleRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.MarkReleasedAsync(1, 1, null));
        }

        // ─── ForceCompleteAsync ───────────────────────────────────────────────

        [Fact]
        public async Task ForceCompleteAsync_WhenDescriptionEmpty_ThrowsBusinessRule()
        {
            var service = CreateService();

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.ForceCompleteAsync(1, 1, null, ""));
        }

        [Fact]
        public async Task ForceCompleteAsync_WhenDescriptionWhitespace_ThrowsBusinessRule()
        {
            var service = CreateService();

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.ForceCompleteAsync(1, 1, null, "   "));
        }

        [Fact]
        public async Task ForceCompleteAsync_WhenConnectionFails_Throws()
        {
            _scheduleRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ForceCompleteAsync(1, 1, null, "reason"));
        }

        [Fact]
        public async Task ForceCompleteAsync_WhenConnected_DelegatesToRepository()
        {
            _scheduleRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            var service = CreateService();

            await service.ForceCompleteAsync(42, 7, 30, "reason");

            _scheduleRep.Verify(r => r.ForceCompleteAsync(42, 7, It.IsAny<DateTime>(), 30, "reason"), Times.Once);
        }

        // ─── SplitScheduleAsync ───────────────────────────────────────────────

        [Fact]
        public async Task SplitScheduleAsync_WhenRemainingQtyZero_ThrowsBusinessRule()
        {
            var service = CreateService();

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.SplitScheduleAsync(new ScheduleSplitDto { OriginalScheduleId = 1, RemainingQuantity = 0, ReleasedBy = 1 }));
        }

        [Fact]
        public async Task SplitScheduleAsync_WhenRemainingQtyNegative_ThrowsBusinessRule()
        {
            var service = CreateService();

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.SplitScheduleAsync(new ScheduleSplitDto { OriginalScheduleId = 1, RemainingQuantity = -5, ReleasedBy = 1 }));
        }

        [Fact]
        public async Task SplitScheduleAsync_WhenConnectionFails_Throws()
        {
            _scheduleRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SplitScheduleAsync(new ScheduleSplitDto { OriginalScheduleId = 1, RemainingQuantity = 40, ReleasedBy = 1 }));
        }

        [Fact]
        public async Task SplitScheduleAsync_WhenConnected_DelegatesToRepository()
        {
            _scheduleRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            var service = CreateService();

            await service.SplitScheduleAsync(new ScheduleSplitDto
            {
                OriginalScheduleId = 5, RemainingQuantity = 40, ReleasedBy = 3, Description = "note"
            });

            _scheduleRep.Verify(r => r.SplitScheduleAsync(5, 40, 3, It.IsAny<DateTime>(), "note"), Times.Once);
        }
    }
}
