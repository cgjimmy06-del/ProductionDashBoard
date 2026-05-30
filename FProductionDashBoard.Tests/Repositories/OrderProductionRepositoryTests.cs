using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.Repositories
{
    public class OrderProductionRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<MesDbContext> _options;

        public OrderProductionRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<MesDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var ctx = new MesDbContext(_options);
            ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
            ctx.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        [Fact]
        public async Task StartAsync_WhenOrderNotFound_ThrowsInvalidOperation()
        {
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.StartAsync(9999, 1, DateTime.Today));
            Assert.Contains("找不到", ex.Message);
        }

        [Fact]
        public async Task StartAsync_WhenPending_TransitionsToInProduction()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.Pending);
            var repository = CreateRepository();

            await repository.StartAsync(orderId, 7, new DateTime(2026, 5, 30, 8, 0, 0));

            using var ctx = new MesDbContext(_options);
            var order = await ctx.OrderProductions.FindAsync(orderId);
            Assert.Equal(OrderProductionStatus.InProduction, order!.Status);
        }

        [Fact]
        public async Task StartAsync_WhenPending_SetsStartedByAndStartedAt()
        {
            var startedAt = new DateTime(2026, 5, 30, 8, 0, 0);
            var orderId = await InsertOrderAsync(OrderProductionStatus.Pending);
            var repository = CreateRepository();

            await repository.StartAsync(orderId, 7, startedAt);

            using var ctx = new MesDbContext(_options);
            var order = await ctx.OrderProductions.FindAsync(orderId);
            Assert.Equal(7, order!.StartedBy);
            Assert.Equal(startedAt, order.StartedAt);
        }

        [Fact]
        public async Task StartAsync_WhenInProduction_ThrowsInvalidOperation()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.InProduction);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.StartAsync(orderId, 1, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task StartAsync_WhenCompleted_ThrowsInvalidOperation()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.Completed);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.StartAsync(orderId, 1, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task StartAsync_WhenCancelled_ThrowsInvalidOperation()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.Cancelled);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.StartAsync(orderId, 1, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task EndAsync_WhenOrderNotFound_ThrowsInvalidOperation()
        {
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.EndAsync(9999, DateTime.Today));
            Assert.Contains("找不到", ex.Message);
        }

        [Fact]
        public async Task EndAsync_WhenInProduction_TransitionsToCompleted()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.InProduction);
            var repository = CreateRepository();

            await repository.EndAsync(orderId, new DateTime(2026, 5, 30, 17, 0, 0));

            using var ctx = new MesDbContext(_options);
            var order = await ctx.OrderProductions.FindAsync(orderId);
            Assert.Equal(OrderProductionStatus.Completed, order!.Status);
        }

        [Fact]
        public async Task EndAsync_WhenPending_ThrowsInvalidOperation()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.Pending);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.EndAsync(orderId, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task EndAsync_WhenCompleted_ThrowsInvalidOperation()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.Completed);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.EndAsync(orderId, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task EndAsync_WhenCancelled_ThrowsInvalidOperation()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.Cancelled);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.EndAsync(orderId, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task CancelAsync_WhenOrderNotFound_ThrowsInvalidOperation()
        {
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.CancelAsync(9999, "cancelled"));
            Assert.Contains("找不到", ex.Message);
        }

        [Fact]
        public async Task CancelAsync_WhenPending_TransitionsToCancelled()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.Pending);
            var repository = CreateRepository();

            await repository.CancelAsync(orderId, "cancelled");

            using var ctx = new MesDbContext(_options);
            var order = await ctx.OrderProductions.FindAsync(orderId);
            Assert.Equal(OrderProductionStatus.Cancelled, order!.Status);
        }

        [Fact]
        public async Task CancelAsync_WhenInProduction_TransitionsToCancelled()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.InProduction);
            var repository = CreateRepository();

            await repository.CancelAsync(orderId, "cancelled");

            using var ctx = new MesDbContext(_options);
            var order = await ctx.OrderProductions.FindAsync(orderId);
            Assert.Equal(OrderProductionStatus.Cancelled, order!.Status);
        }

        [Fact]
        public async Task CancelAsync_WhenCompleted_ThrowsInvalidOperation()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.Completed);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.CancelAsync(orderId, "cancelled"));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task CancelAsync_WhenCancelled_ThrowsInvalidOperation()
        {
            var orderId = await InsertOrderAsync(OrderProductionStatus.Cancelled);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.CancelAsync(orderId, "cancelled"));
            Assert.Contains("狀態不允許", ex.Message);
        }

        private OrderProductionRepository CreateRepository() => new(CreateFactory());

        private IDbContextFactory<MesDbContext> CreateFactory()
        {
            var mock = new Mock<IDbContextFactory<MesDbContext>>();
            mock.Setup(f => f.CreateDbContext())
                .Returns(() => new MesDbContext(_options));
            mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MesDbContext(_options));
            return mock.Object;
        }

        private async Task<int> InsertOrderAsync(OrderProductionStatus status)
        {
            using var ctx = new MesDbContext(_options);
            var order = new OrderProduction
            {
                EquipmentId = 1,
                EquipmentProductId = 1,
                Status = status,
                Quantity = 10,
                CreatedBy = 1,
                CreateAt = DateTime.Today,
                UpdateAt = DateTime.Today
            };

            ctx.OrderProductions.Add(order);
            await ctx.SaveChangesAsync();
            return order.OrderId;
        }
    }
}
