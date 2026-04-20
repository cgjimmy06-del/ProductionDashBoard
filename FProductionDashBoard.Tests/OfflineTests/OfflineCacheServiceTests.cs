using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Offline;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FProductionDashBoard.Tests.OfflineTests
{
    public class OfflineCacheServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly LocalDbContext _db;
        private readonly OfflineCacheService _sut;

        public OfflineCacheServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<LocalDbContext>()
                .UseSqlite(_connection)
                .Options;

            _db = new LocalDbContext(options);
            _sut = new OfflineCacheService(_db);
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }

        private static PendingOperation MakePending(PendingOperationType type = PendingOperationType.AddReplacement)
            => new() { OperationType = type, PayloadJson = "{}" };

        // ─── EnqueueAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task EnqueueAsync_PersistsOperation()
        {
            var op = MakePending();
            await _sut.EnqueueAsync(op);

            var stored = await _db.PendingOperations.FindAsync(op.Id);
            Assert.NotNull(stored);
            Assert.Equal(0, stored.RetryCount);
        }

        [Fact]
        public async Task EnqueueAsync_DefaultRetryCount_IsZero()
        {
            var op = MakePending();
            await _sut.EnqueueAsync(op);

            Assert.Equal(0, op.RetryCount);
        }

        // ─── HasPendingAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task HasPendingAsync_WhenEmpty_ReturnsFalse()
        {
            Assert.False(await _sut.HasPendingAsync());
        }

        [Fact]
        public async Task HasPendingAsync_WhenExists_ReturnsTrue()
        {
            await _sut.EnqueueAsync(MakePending());
            Assert.True(await _sut.HasPendingAsync());
        }

        // ─── GetPendingAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetPendingAsync_ReturnsAllOperations()
        {
            var op1 = MakePending();
            var op2 = MakePending(PendingOperationType.AddFirstInspection);

            await _sut.EnqueueAsync(op1);
            await _sut.EnqueueAsync(op2);

            var result = await _sut.GetPendingAsync();
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetPendingAsync_OrderedByCreatedAt()
        {
            var first = new PendingOperation
            {
                OperationType = PendingOperationType.AddReplacement,
                PayloadJson = "{}",
                CreatedAt = DateTime.Now.AddMinutes(-5)
            };
            var second = new PendingOperation
            {
                OperationType = PendingOperationType.AddFirstInspection,
                PayloadJson = "{}",
                CreatedAt = DateTime.Now
            };

            await _sut.EnqueueAsync(second);
            await _sut.EnqueueAsync(first);

            var result = await _sut.GetPendingAsync();
            Assert.Equal(first.Id, result[0].Id);
        }

        // ─── MarkSyncedAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task MarkSyncedAsync_RemovesOperation()
        {
            var op = MakePending();
            await _sut.EnqueueAsync(op);

            await _sut.MarkSyncedAsync(op.Id);

            Assert.Null(await _db.PendingOperations.FindAsync(op.Id));
        }

        // ─── MarkFailedAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task MarkFailedAsync_IncrementsRetryCount()
        {
            var op = MakePending();
            await _sut.EnqueueAsync(op);

            await _sut.MarkFailedAsync(op.Id);

            var updated = await _db.PendingOperations.FindAsync(op.Id);
            Assert.Equal(1, updated!.RetryCount);
            Assert.NotNull(updated.LastAttemptAt);
        }

        [Fact]
        public async Task MarkFailedAsync_MultipleRetries_KeepsIncrementing()
        {
            var op = MakePending();
            await _sut.EnqueueAsync(op);

            await _sut.MarkFailedAsync(op.Id);
            await _sut.MarkFailedAsync(op.Id);
            await _sut.MarkFailedAsync(op.Id);

            var updated = await _db.PendingOperations.FindAsync(op.Id);
            Assert.Equal(3, updated!.RetryCount);
        }
    }
}
