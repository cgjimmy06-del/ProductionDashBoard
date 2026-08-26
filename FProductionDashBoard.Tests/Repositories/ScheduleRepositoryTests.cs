using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Exceptions;
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

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => repository.MarkScheduledAsync(id, 1, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task MarkScheduledAsync_WhenCompleted_ThrowsInvalidOperation()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Completed);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
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

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
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

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
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

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => repository.MarkCancelledAsync(id, "test"));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task MarkCancelledAsync_WhenCancelled_ThrowsInvalidOperation()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Cancelled);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => repository.MarkCancelledAsync(id, "test"));
            Assert.Contains("狀態不允許", ex.Message);
        }

        // ─── ForceCompleteAsync ───────────────────────────────────────────────

        [Fact]
        public async Task ForceCompleteAsync_WhenPending_TransitionsToCompleted()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Pending, quantity: 50);
            var repository = CreateRepository();

            await repository.ForceCompleteAsync(id, 9, DateTime.Today, 40, "強制完成");

            using var ctx = new MesDbContext(_options);
            var s = await ctx.Schedules.FindAsync(id);
            Assert.Equal(ScheduleStatus.Completed, s!.Status);
            Assert.Equal(9, s.VerifiedBy);
            Assert.Equal(40, s.ActualQuantity);
            Assert.Equal("強制完成", s.Description);
        }

        [Fact]
        public async Task ForceCompleteAsync_WhenActualQtyNull_UsesOriginalQuantity()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Pending, quantity: 70);
            var repository = CreateRepository();

            await repository.ForceCompleteAsync(id, 9, DateTime.Today, null, "強制完成");

            using var ctx = new MesDbContext(_options);
            var s = await ctx.Schedules.FindAsync(id);
            Assert.Equal(70, s!.ActualQuantity);
        }

        [Fact]
        public async Task ForceCompleteAsync_WhenNotPending_ThrowsBusinessRule()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Scheduled);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => repository.ForceCompleteAsync(id, 1, DateTime.Today, null, "x"));
            Assert.Contains("狀態不允許", ex.Message);
        }

        // ─── SplitScheduleAsync ───────────────────────────────────────────────

        [Fact]
        public async Task SplitScheduleAsync_WhenCompleted_ReleasesOriginalAndCreatesChild()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Completed, quantity: 100, actualQuantity: 60);
            var repository = CreateRepository();

            await repository.SplitScheduleAsync(id, 40, 3, DateTime.Today, null);

            using var ctx = new MesDbContext(_options);
            var original = await ctx.Schedules.FindAsync(id);
            Assert.Equal(ScheduleStatus.Released, original!.Status);
            Assert.Equal(3, original.ReleasedBy);

            var child = ctx.Schedules.Single(s => s.ParentId == id);
            Assert.Equal(ScheduleStatus.Pending, child.Status);
            Assert.Equal(40, child.Quantity);
            Assert.Equal(original.ProductId, child.ProductId);
        }

        [Fact]
        public async Task SplitScheduleAsync_ReturnsNewChildScheduleId()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Completed, quantity: 100, actualQuantity: 60);
            var repository = CreateRepository();

            var childId = await repository.SplitScheduleAsync(id, 40, 3, DateTime.Today, null);

            using var ctx = new MesDbContext(_options);
            var child = ctx.Schedules.Single(s => s.ParentId == id);
            Assert.True(childId > 0);
            Assert.Equal(child.ScheduleId, childId);   // 子單沿用原倉位需要 child id
        }

        [Fact]
        public async Task SplitScheduleAsync_WhenQtySumMismatch_ThrowsBusinessRule()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Completed, quantity: 100, actualQuantity: 60);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => repository.SplitScheduleAsync(id, 30, 3, DateTime.Today, null)); // 60 + 30 != 100
            Assert.Contains("必須等於原始數量", ex.Message);
        }

        [Fact]
        public async Task SplitScheduleAsync_WhenNotCompleted_ThrowsBusinessRule()
        {
            var id = await InsertScheduleAsync(ScheduleStatus.Pending);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => repository.SplitScheduleAsync(id, 5, 3, DateTime.Today, null));
            Assert.Contains("不允許拆單", ex.Message);
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

        private async Task<int> InsertScheduleAsync(ScheduleStatus status, int quantity = 10, int? actualQuantity = null)
        {
            using var ctx = new MesDbContext(_options);
            var schedule = new Schedule
            {
                ProductId      = 1,
                ProcessId      = 1,
                Quantity       = quantity,
                ActualQuantity = actualQuantity,
                Status         = status,
                ReceivedBy     = 1,
                ReceivedAt     = DateTime.Today,
                CreateAt       = DateTime.Today,
                UpdateAt       = DateTime.Today
            };
            ctx.Schedules.Add(schedule);
            await ctx.SaveChangesAsync();
            return schedule.ScheduleId;
        }
    }
}
