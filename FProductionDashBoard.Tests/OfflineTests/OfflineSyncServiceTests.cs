using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services.Offline.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.OfflineTests
{
    public class OfflineSyncServiceTests
    {
        private readonly Mock<IOfflineCacheService> _cache = new();
        private readonly Mock<IEquipmentRepository> _equipmentRep = new();
        private readonly Mock<IPendingOperationHandler> _handler = new();
        private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
        private readonly Mock<IServiceScope> _scope = new();
        private readonly Mock<IServiceProvider> _sp = new();

        public OfflineSyncServiceTests()
        {
            _scopeFactory.Setup(f => f.CreateScope()).Returns(_scope.Object);
            _scope.Setup(s => s.ServiceProvider).Returns(_sp.Object);
            _sp.Setup(p => p.GetService(typeof(IEquipmentRepository))).Returns(_equipmentRep.Object);
            _sp.Setup(p => p.GetService(typeof(IEnumerable<IPendingOperationHandler>)))
               .Returns(new[] { _handler.Object });
        }

        private OfflineSyncService CreateService() =>
            new OfflineSyncService(_cache.Object, _scopeFactory.Object);

        [Fact]
        public async Task SyncPendingAsync_WhenNoPending_ReturnsEmptyAndDoesNotCreateScope()
        {
            _cache.Setup(c => c.HasPendingAsync()).ReturnsAsync(false);
            var sut = CreateService();

            var result = await sut.SyncPendingAsync();

            Assert.Equal(0, result.SyncedCount);
            Assert.Equal(0, result.FailedCount);
            _scopeFactory.Verify(f => f.CreateScope(), Times.Never);
        }

        [Fact]
        public async Task SyncPendingAsync_WhenConnectionFails_ReturnsEmptyAndDoesNotProcessOps()
        {
            _cache.Setup(c => c.HasPendingAsync()).ReturnsAsync(true);
            _equipmentRep.Setup(r => r.CheckConnection()).Returns(false);
            var sut = CreateService();

            var result = await sut.SyncPendingAsync();

            Assert.Equal(0, result.SyncedCount);
            _cache.Verify(c => c.GetPendingAsync(), Times.Never);
        }

        [Fact]
        public async Task SyncPendingAsync_WhenHandlerSucceeds_ReturnsSyncedCountOne()
        {
            var op = new PendingOperation { OperationType = PendingOperationType.AddReplacement, PayloadJson = "{}" };
            _cache.Setup(c => c.HasPendingAsync()).ReturnsAsync(true);
            _cache.Setup(c => c.GetPendingAsync()).ReturnsAsync(new List<PendingOperation> { op });
            _cache.Setup(c => c.MarkSyncedAsync(op.Id)).Returns(Task.CompletedTask);
            _equipmentRep.Setup(r => r.CheckConnection()).Returns(true);
            _handler.Setup(h => h.OperationType).Returns(PendingOperationType.AddReplacement);
            _handler.Setup(h => h.HandleAsync(op)).Returns(Task.CompletedTask);

            var sut = CreateService();
            var result = await sut.SyncPendingAsync();

            Assert.Equal(1, result.SyncedCount);
            Assert.Equal(0, result.FailedCount);
            _cache.Verify(c => c.MarkSyncedAsync(op.Id), Times.Once);
        }

        [Fact]
        public async Task SyncPendingAsync_WhenHandlerFails_ReturnsFailedCountAndContinues()
        {
            var op1 = new PendingOperation { OperationType = PendingOperationType.AddReplacement, PayloadJson = "{}" };
            var op2 = new PendingOperation { OperationType = PendingOperationType.AddFirstInspection, PayloadJson = "{}" };
            _cache.Setup(c => c.HasPendingAsync()).ReturnsAsync(true);
            _cache.Setup(c => c.GetPendingAsync()).ReturnsAsync(new List<PendingOperation> { op1, op2 });
            _cache.Setup(c => c.MarkSyncedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
            _cache.Setup(c => c.MarkFailedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
            _equipmentRep.Setup(r => r.CheckConnection()).Returns(true);

            var handler1 = new Mock<IPendingOperationHandler>();
            handler1.Setup(h => h.OperationType).Returns(PendingOperationType.AddReplacement);
            handler1.Setup(h => h.HandleAsync(op1)).ThrowsAsync(new Exception("fail"));

            var handler2 = new Mock<IPendingOperationHandler>();
            handler2.Setup(h => h.OperationType).Returns(PendingOperationType.AddFirstInspection);
            handler2.Setup(h => h.HandleAsync(op2)).Returns(Task.CompletedTask);

            _sp.Setup(p => p.GetService(typeof(IEnumerable<IPendingOperationHandler>)))
               .Returns(new[] { handler1.Object, handler2.Object });

            var sut = new OfflineSyncService(_cache.Object, _scopeFactory.Object);
            var result = await sut.SyncPendingAsync();

            Assert.Equal(1, result.SyncedCount);
            Assert.Equal(1, result.FailedCount);
            _cache.Verify(c => c.MarkFailedAsync(op1.Id), Times.Once);
            _cache.Verify(c => c.MarkSyncedAsync(op2.Id), Times.Once);
        }

        [Fact]
        public async Task SyncPendingAsync_WhenNoHandlerFound_SkipsOperation()
        {
            var op = new PendingOperation { OperationType = PendingOperationType.AddReplacement, PayloadJson = "{}" };
            _cache.Setup(c => c.HasPendingAsync()).ReturnsAsync(true);
            _cache.Setup(c => c.GetPendingAsync()).ReturnsAsync(new List<PendingOperation> { op });
            _equipmentRep.Setup(r => r.CheckConnection()).Returns(true);
            _handler.Setup(h => h.OperationType).Returns(PendingOperationType.AddFirstInspection);

            var sut = CreateService();
            var result = await sut.SyncPendingAsync();

            Assert.Equal(0, result.SyncedCount);
            Assert.Equal(0, result.FailedCount);
            _cache.Verify(c => c.MarkSyncedAsync(It.IsAny<Guid>()), Times.Never);
            _cache.Verify(c => c.MarkFailedAsync(It.IsAny<Guid>()), Times.Never);
        }
    }
}
