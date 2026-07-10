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

        [Fact]
        public async Task StartAsync_WritesStartedByAndManagedBy()
        {
            var equipmentProductId = await InsertEquipmentProductAsync();
            var repository = CreateRepository();

            var recordId = await repository.StartAsync(
                1, equipmentProductId, TuningType.Teaching,
                startedBy: 7, managedBy: 3,
                new DateTime(2026, 5, 30, 8, 0, 0), forceStatus: false);

            using var ctx = new MesDbContext(_options);
            var record = await ctx.ProgramTuningRecords.FindAsync(recordId);
            Assert.Equal(7, record!.StartedBy);
            Assert.Equal(3, record.ManagedBy);
        }

        [Fact]
        public async Task StartAsync_WhenForceStatus_ChangesEquipmentProductStatus()
        {
            var equipmentProductId = await InsertEquipmentProductAsync(TuningType.Feasible);
            var repository = CreateRepository();

            await repository.StartAsync(
                1, equipmentProductId, TuningType.Teaching,
                startedBy: 1, managedBy: 1,
                new DateTime(2026, 5, 30, 8, 0, 0), forceStatus: true);

            using var ctx = new MesDbContext(_options);
            var equipmentProduct = await ctx.EquipmentProducts.FindAsync(equipmentProductId);
            Assert.Equal(TuningType.Teaching, equipmentProduct!.ProductionStatus);
        }

        [Fact]
        public async Task StartAsync_WhenNotForceStatus_LeavesEquipmentProductStatus()
        {
            var equipmentProductId = await InsertEquipmentProductAsync(TuningType.Feasible);
            var repository = CreateRepository();

            await repository.StartAsync(
                1, equipmentProductId, TuningType.Teaching,
                startedBy: 1, managedBy: 1,
                new DateTime(2026, 5, 30, 8, 0, 0), forceStatus: false);

            using var ctx = new MesDbContext(_options);
            var equipmentProduct = await ctx.EquipmentProducts.FindAsync(equipmentProductId);
            Assert.Equal(TuningType.Feasible, equipmentProduct!.ProductionStatus);
        }

        [Fact]
        public async Task GetLastCompletedTeachingNamesByEquipmentAsync_ReturnsLatestExecutorPerProduct()
        {
            var equipmentProductId = await InsertEquipmentProductAsync();
            await InsertEmployeeAsync(10, "Alice");
            await InsertEmployeeAsync(20, "Bob");
            // 較早的一筆帶點
            await InsertTeachingHistoryAsync(equipmentProductId, startedBy: 10, endedAt: new DateTime(2026, 5, 1),
                status: ProgramTuningStatus.Completed, type: TuningType.Teaching);
            // 較晚的一筆帶點（應取此人）
            await InsertTeachingHistoryAsync(equipmentProductId, startedBy: 20, endedAt: new DateTime(2026, 6, 1),
                status: ProgramTuningStatus.Completed, type: TuningType.Teaching);
            // 更晚但為 Offset（應排除）
            await InsertTeachingHistoryAsync(equipmentProductId, startedBy: 10, endedAt: new DateTime(2026, 7, 1),
                status: ProgramTuningStatus.Completed, type: TuningType.Offset);
            // 進行中帶點（應排除）
            await InsertTeachingHistoryAsync(equipmentProductId, startedBy: 10, endedAt: null,
                status: ProgramTuningStatus.InProgress, type: TuningType.Teaching);
            var repository = CreateRepository();

            var map = await repository.GetLastCompletedTeachingNamesByEquipmentAsync(1);

            Assert.Equal("Bob", map[equipmentProductId]);
        }

        [Fact]
        public async Task GetLastCompletedTeachingNamesByEquipmentAsync_WhenNoTeaching_ReturnsEmptyMap()
        {
            var equipmentProductId = await InsertEquipmentProductAsync();
            var repository = CreateRepository();

            var map = await repository.GetLastCompletedTeachingNamesByEquipmentAsync(1);

            Assert.Empty(map);
            Assert.False(map.ContainsKey(equipmentProductId));
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

        private async Task<int> InsertEquipmentProductAsync(TuningType initialStatus = TuningType.Teaching)
        {
            using var ctx = new MesDbContext(_options);
            var equipmentProduct = new EquipmentProduct
            {
                EquipmentId = 1,
                SeqNo = 1,
                SopId = 1,
                ProductionStatus = initialStatus,
                CreateAt = DateTime.Today,
                UpdateAt = DateTime.Today
            };

            ctx.EquipmentProducts.Add(equipmentProduct);
            await ctx.SaveChangesAsync();
            return equipmentProduct.EquipmentProductId;
        }

        private async Task InsertEmployeeAsync(int employeeId, string name)
        {
            using var ctx = new MesDbContext(_options);
            ctx.Employees.Add(new Employee
            {
                EmployeeId = employeeId,
                UserId = $"user{employeeId}",
                Name = name,
                RoleId = 1,
                CreateAt = DateTime.Today,
                UpdateAt = DateTime.Today
            });
            await ctx.SaveChangesAsync();
        }

        private async Task InsertTeachingHistoryAsync(int equipmentProductId, int startedBy, DateTime? endedAt,
            ProgramTuningStatus status, TuningType type)
        {
            using var ctx = new MesDbContext(_options);
            ctx.ProgramTuningRecords.Add(new ProgramTuningRecord
            {
                EquipmentId = 1,
                EquipmentProductId = equipmentProductId,
                TuningType = type,
                Status = status,
                StartedBy = startedBy,
                StartedAt = new DateTime(2026, 1, 1, 8, 0, 0),
                EndedAt = endedAt,
                CreateAt = DateTime.Today,
                UpdateAt = DateTime.Today
            });
            await ctx.SaveChangesAsync();
        }
    }
}
