using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.Repositories
{
    public class ScheduleRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<MesDbContext> _options;

        public ScheduleRepositoryTests()
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

        // ─── MarkScheduledAsync ───────────────────────────────────────────────

        [Fact]
        public async Task MarkScheduledAsync_WhenNotFound_ThrowsInvalidOperation()
        {
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.MarkScheduledAsync(9999, 1, DateTime.Today));
            Assert.Contains("找不到", ex.Message);
        }

        [Fact]
        public async Task MarkScheduledAsync_WhenPending_TransitionsToScheduled()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Pending);
            var repository = CreateRepository();

            await repository.MarkScheduledAsync(id, 7, DateTime.Today);

            using var ctx = new MesDbContext(_options);
            var s = await ctx.Schedules.FindAsync(id);
            Assert.Equal(ScheduleStatus.Scheduled, s!.Status);
            Assert.Equal(7, s.ScheduledBy);
        }

        [Fact]
        public async Task MarkScheduledAsync_WhenScheduled_ThrowsInvalidOperation()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Scheduled);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.MarkScheduledAsync(id, 1, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task MarkScheduledAsync_WhenCompleted_ThrowsInvalidOperation()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Completed);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.MarkScheduledAsync(id, 1, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        // ─── MarkVerifiedAsync ────────────────────────────────────────────────

        [Fact]
        public async Task MarkVerifiedAsync_WhenScheduled_TransitionsToCompleted()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Scheduled);
            var repository = CreateRepository();

            await repository.MarkVerifiedAsync(id, 5, DateTime.Today, 80, null);

            using var ctx = new MesDbContext(_options);
            var s = await ctx.Schedules.FindAsync(id);
            Assert.Equal(ScheduleStatus.Completed, s!.Status);
            Assert.Equal(5, s.VerifiedBy);
            Assert.Equal(80, s.ActualQuantity);
        }

        [Fact]
        public async Task MarkVerifiedAsync_WhenActualQtyNull_UsesOriginalQuantity()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Scheduled, quantity: 100);
            var repository = CreateRepository();

            await repository.MarkVerifiedAsync(id, 5, DateTime.Today, null, null);

            using var ctx = new MesDbContext(_options);
            var s = await ctx.Schedules.FindAsync(id);
            Assert.Equal(100, s!.ActualQuantity);
        }

        [Fact]
        public async Task MarkVerifiedAsync_WhenPending_ThrowsInvalidOperation()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Pending);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.MarkVerifiedAsync(id, 1, DateTime.Today, null, null));
            Assert.Contains("狀態不允許", ex.Message);
        }

        // ─── MarkReleasedAsync ────────────────────────────────────────────────

        [Fact]
        public async Task MarkReleasedAsync_WhenCompleted_TransitionsToReleased()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Completed);
            var repository = CreateRepository();

            await repository.MarkReleasedAsync(id, 3, DateTime.Today, null);

            using var ctx = new MesDbContext(_options);
            var s = await ctx.Schedules.FindAsync(id);
            Assert.Equal(ScheduleStatus.Released, s!.Status);
            Assert.Equal(3, s.ReleasedBy);
        }

        [Fact]
        public async Task MarkReleasedAsync_WhenScheduled_ThrowsInvalidOperation()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Scheduled);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.MarkReleasedAsync(id, 1, DateTime.Today, null));
            Assert.Contains("狀態不允許", ex.Message);
        }

        // ─── MarkCancelledAsync ───────────────────────────────────────────────

        [Fact]
        public async Task MarkCancelledAsync_WhenPending_TransitionsToCancelled()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Pending);
            var repository = CreateRepository();

            await repository.MarkCancelledAsync(id, "test");

            using var ctx = new MesDbContext(_options);
            var s = await ctx.Schedules.FindAsync(id);
            Assert.Equal(ScheduleStatus.Cancelled, s!.Status);
        }

        [Fact]
        public async Task MarkCancelledAsync_WhenScheduled_TransitionsToCancelled()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Scheduled);
            var repository = CreateRepository();

            await repository.MarkCancelledAsync(id, "test");

            using var ctx = new MesDbContext(_options);
            var s = await ctx.Schedules.FindAsync(id);
            Assert.Equal(ScheduleStatus.Cancelled, s!.Status);
        }

        [Fact]
        public async Task MarkCancelledAsync_WhenReleased_ThrowsInvalidOperation()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Released);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.MarkCancelledAsync(id, "test"));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task MarkCancelledAsync_WhenCancelled_ThrowsInvalidOperation()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Cancelled);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.MarkCancelledAsync(id, "test"));
            Assert.Contains("狀態不允許", ex.Message);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private ScheduleRepository CreateRepository() => new(CreateFactory());

        private IDbContextFactory<MesDbContext> CreateFactory()
        {
            var mock = new Mock<IDbContextFactory<MesDbContext>>();
            mock.Setup(f => f.CreateDbContext())
                .Returns(() => new MesDbContext(_options));
            mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MesDbContext(_options));
            return mock.Object;
        }

        private async Task<int> InsertScheduleAsync(ScheduleStatus status, int quantity = 10)
        {
            using var ctx = new MesDbContext(_options);
            var schedule = new Schedule
            {
                ProductId  = 1,
                ProcessId  = 1,
                Quantity   = quantity,
                Status     = status,
                ReceivedBy = 1,
                ReceivedAt = DateTime.Today,
                CreateAt   = DateTime.Today,
                UpdateAt   = DateTime.Today
            };
            ctx.Schedules.Add(schedule);
            await ctx.SaveChangesAsync();
            return schedule.ScheduleId;
        }
    }
}
