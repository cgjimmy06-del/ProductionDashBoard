using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.Exceptions;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class WarehouseServiceTests
    {
        private readonly Mock<IWarehouseRepository> _warehouseRep = new();

        private WarehouseService CreateService() => new(_warehouseRep.Object);

        // ─── 連線守衛 ───────────────────────────────────────────────────────────

        [Fact]
        public async Task GetLocationsAsync_WhenConnectionFails_Throws()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetLocationsAsync());
        }

        [Fact]
        public async Task AddLocationAsync_WhenConnectionFails_Throws()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddLocationAsync(new StorageLocation { Code = "A-01" }));
        }

        // ─── 業務規則 ───────────────────────────────────────────────────────────

        [Fact]
        public async Task AddLocationAsync_WhenCodeExists_ThrowsBusinessRule()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _warehouseRep.Setup(r => r.ExistsLocationCodeAsync("A-01", null)).ReturnsAsync(true);
            var service = CreateService();

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.AddLocationAsync(new StorageLocation { Code = "A-01" }));
            _warehouseRep.Verify(r => r.AddAsync(It.IsAny<StorageLocation>()), Times.Never);
        }

        [Fact]
        public async Task AddLocationAsync_WhenValid_DelegatesToRepo()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _warehouseRep.Setup(r => r.ExistsLocationCodeAsync("A-01", null)).ReturnsAsync(false);
            var service = CreateService();
            var location = new StorageLocation { Code = "A-01" };

            await service.AddLocationAsync(location);

            _warehouseRep.Verify(r => r.AddAsync(location), Times.Once);
        }

        [Fact]
        public async Task UpdateLocationAsync_WhenCodeExistsOnOther_ThrowsBusinessRule()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _warehouseRep.Setup(r => r.ExistsLocationCodeAsync("A-01", 5)).ReturnsAsync(true);
            var service = CreateService();

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.UpdateLocationAsync(new StorageLocation { LocationId = 5, Code = "A-01" }));
            _warehouseRep.Verify(r => r.UpdateAsync(It.IsAny<StorageLocation>()), Times.Never);
        }

        [Fact]
        public async Task UpdateLocationAsync_WhenValid_StampsUpdateAtAndDelegates()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _warehouseRep.Setup(r => r.ExistsLocationCodeAsync("A-01", 5)).ReturnsAsync(false);
            var service = CreateService();
            var location = new StorageLocation { LocationId = 5, Code = "A-01" };

            await service.UpdateLocationAsync(location);

            Assert.NotNull(location.UpdateAt);
            _warehouseRep.Verify(r => r.UpdateAsync(location), Times.Once);
        }

        [Fact]
        public async Task DeleteLocationAsync_WhenHasActive_ThrowsBusinessRule()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _warehouseRep.Setup(r => r.HasActiveAssignmentsAtLocationAsync(5)).ReturnsAsync(true);
            var service = CreateService();

            await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteLocationAsync(5));
            _warehouseRep.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteLocationAsync_WhenClear_DelegatesToRepo()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _warehouseRep.Setup(r => r.HasActiveAssignmentsAtLocationAsync(5)).ReturnsAsync(false);
            var service = CreateService();

            await service.DeleteLocationAsync(5);

            _warehouseRep.Verify(r => r.DeleteAsync(5), Times.Once);
        }

        [Fact]
        public async Task AssignAsync_WhenAlreadyActive_ThrowsBusinessRule()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _warehouseRep.Setup(r => r.HasActiveAssignmentAsync(100)).ReturnsAsync(true);
            var service = CreateService();

            await Assert.ThrowsAsync<BusinessRuleException>(() => service.AssignAsync(1, 100, 1));
            _warehouseRep.Verify(r => r.AssignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task AssignAsync_WhenValid_ReturnsRepoAssignmentId()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(true);
            _warehouseRep.Setup(r => r.HasActiveAssignmentAsync(100)).ReturnsAsync(false);
            _warehouseRep.Setup(r => r.AssignAsync(1, 100, 7)).ReturnsAsync(42);
            var service = CreateService();

            var id = await service.AssignAsync(1, 100, 7);

            Assert.Equal(42, id);
            _warehouseRep.Verify(r => r.AssignAsync(1, 100, 7), Times.Once);
        }

        [Fact]
        public async Task ReleaseAsync_WhenConnectionFails_Throws()
        {
            _warehouseRep.Setup(r => r.CheckConnectionAsync()).ReturnsAsync(false);
            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReleaseAsync(100, 1));
        }
    }
}
