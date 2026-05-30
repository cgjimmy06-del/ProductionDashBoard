using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.Repositories
{
    public class ProgramTuningRecordRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<MesDbContext> _options;

        public ProgramTuningRecordRepositoryTests()
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
        public async Task EndAsync_WhenRecordNotFound_ThrowsInvalidOperation()
        {
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.EndAsync(9999, DateTime.Today));
            Assert.Contains("找不到", ex.Message);
        }

        [Fact]
        public async Task EndAsync_WhenInProgress_TransitionsToCompleted()
        {
            var equipmentProductId = await InsertEquipmentProductAsync();
            var recordId = await InsertRecordAsync(ProgramTuningStatus.InProgress, equipmentProductId);
            var repository = CreateRepository();

            await repository.EndAsync(recordId, new DateTime(2026, 5, 30, 17, 0, 0));

            using var ctx = new MesDbContext(_options);
            var record = await ctx.ProgramTuningRecords.FindAsync(recordId);
            Assert.Equal(ProgramTuningStatus.Completed, record!.Status);
        }

        [Fact]
        public async Task EndAsync_WhenInProgress_UpdatesEquipmentProductStatusToPending()
        {
            var equipmentProductId = await InsertEquipmentProductAsync();
            var recordId = await InsertRecordAsync(ProgramTuningStatus.InProgress, equipmentProductId);
            var repository = CreateRepository();

            await repository.EndAsync(recordId, new DateTime(2026, 5, 30, 17, 0, 0));

            using var ctx = new MesDbContext(_options);
            var equipmentProduct = await ctx.EquipmentProducts.FindAsync(equipmentProductId);
            Assert.Equal(TuningType.Pending, equipmentProduct!.ProductionStatus);
        }

        [Fact]
        public async Task EndAsync_WhenInProgressWithoutEquipmentProduct_TransitionsToCompleted()
        {
            var recordId = await InsertRecordAsync(ProgramTuningStatus.InProgress, 9999);
            var repository = CreateRepository();

            await repository.EndAsync(recordId, new DateTime(2026, 5, 30, 17, 0, 0));

            using var ctx = new MesDbContext(_options);
            var record = await ctx.ProgramTuningRecords.FindAsync(recordId);
            Assert.Equal(ProgramTuningStatus.Completed, record!.Status);
        }

        [Fact]
        public async Task EndAsync_WhenCompleted_ThrowsInvalidOperation()
        {
            var equipmentProductId = await InsertEquipmentProductAsync();
            var recordId = await InsertRecordAsync(ProgramTuningStatus.Completed, equipmentProductId);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.EndAsync(recordId, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        [Fact]
        public async Task EndAsync_WhenCancelled_ThrowsInvalidOperation()
        {
            var equipmentProductId = await InsertEquipmentProductAsync();
            var recordId = await InsertRecordAsync(ProgramTuningStatus.Cancelled, equipmentProductId);
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.EndAsync(recordId, DateTime.Today));
            Assert.Contains("狀態不允許", ex.Message);
        }

        private ProgramTuningRecordRepository CreateRepository() => new(CreateFactory());

        private IDbContextFactory<MesDbContext> CreateFactory()
        {
            var mock = new Mock<IDbContextFactory<MesDbContext>>();
            mock.Setup(f => f.CreateDbContext())
                .Returns(() => new MesDbContext(_options));
            mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MesDbContext(_options));
            return mock.Object;
        }

        private async Task<int> InsertRecordAsync(ProgramTuningStatus status, int equipmentProductId)
        {
            using var ctx = new MesDbContext(_options);
            var record = new ProgramTuningRecord
            {
                EquipmentId = 1,
                EquipmentProductId = equipmentProductId,
                TuningType = TuningType.Teaching,
                Status = status,
                StartedBy = 1,
                StartedAt = new DateTime(2026, 5, 30, 8, 0, 0),
                CreateAt = DateTime.Today,
                UpdateAt = DateTime.Today
            };

            ctx.ProgramTuningRecords.Add(record);
            await ctx.SaveChangesAsync();
            return record.ProgramTuningId;
        }

        private async Task<int> InsertEquipmentProductAsync()
        {
            using var ctx = new MesDbContext(_options);
            var equipmentProduct = new EquipmentProduct
            {
                EquipmentId = 1,
                SeqNo = 1,
                SopId = 1,
                ProductionStatus = TuningType.Teaching,
                CreateAt = DateTime.Today,
                UpdateAt = DateTime.Today
            };

            ctx.EquipmentProducts.Add(equipmentProduct);
            await ctx.SaveChangesAsync();
            return equipmentProduct.EquipmentProductId;
        }
    }
}
