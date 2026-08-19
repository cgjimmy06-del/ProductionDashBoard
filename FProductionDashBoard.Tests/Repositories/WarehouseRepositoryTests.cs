using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FProductionDashBoard.Tests.Repositories
{
    public class WarehouseRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<MesDbContext> _options;

        public WarehouseRepositoryTests()
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

        // ─── 倉位主檔 CRUD / 查重 ───────────────────────────────────────────────

        [Fact]
        public async Task AddAsync_ThenGetById_ReturnsLocation()
        {
            var id = await InsertLocationAsync("A-01");
            var repository = CreateRepository();

            var loc = await repository.GetByIdAsync(id);

            Assert.NotNull(loc);
            Assert.Equal("A-01", loc!.Code);
        }

        [Fact]
        public async Task ExistsLocationCodeAsync_WhenExists_ReturnsTrue()
        {
            await InsertLocationAsync("A-01");
            var repository = CreateRepository();

            Assert.True(await repository.ExistsLocationCodeAsync("A-01"));
            Assert.False(await repository.ExistsLocationCodeAsync("A-99"));
        }

        [Fact]
        public async Task ExistsLocationCodeAsync_ExcludingSelf_ReturnsFalse()
        {
            var id = await InsertLocationAsync("A-01");
            var repository = CreateRepository();

            // 更新自身時排除自己，不應誤判為重複
            Assert.False(await repository.ExistsLocationCodeAsync("A-01", id));
        }

        // ─── 上架 / 下架生命週期 ────────────────────────────────────────────────

        [Fact]
        public async Task AssignAsync_ThenActiveCount_IsOne()
        {
            var locId = await InsertLocationAsync("A-01");
            var repository = CreateRepository();

            await repository.AssignAsync(locId, scheduleId: 100, assignedBy: 1);

            Assert.True(await repository.HasActiveAssignmentAsync(100));
            Assert.True(await repository.HasActiveAssignmentsAtLocationAsync(locId));
            var counts = await repository.GetActiveCountByLocationAsync();
            Assert.Equal(1, counts[locId]);
        }

        [Fact]
        public async Task ReleaseAsync_SetsReleasedAt_AndClearsActive()
        {
            var locId = await InsertLocationAsync("A-01");
            var repository = CreateRepository();
            await repository.AssignAsync(locId, scheduleId: 100, assignedBy: 1);

            await repository.ReleaseAsync(scheduleId: 100, releasedBy: 2);

            Assert.False(await repository.HasActiveAssignmentAsync(100));
            Assert.False(await repository.HasActiveAssignmentsAtLocationAsync(locId));
            var counts = await repository.GetActiveCountByLocationAsync();
            Assert.False(counts.ContainsKey(locId));
        }

        [Fact]
        public async Task ReleaseAsync_WhenNoActive_ThrowsInvalidOperation()
        {
            var repository = CreateRepository();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.ReleaseAsync(scheduleId: 999, releasedBy: 1));
            Assert.Contains("找不到現役佔用", ex.Message);
        }

        [Fact]
        public async Task GetActiveAssignmentsAsync_ReturnsOnlyActive_WithScheduleChain()
        {
            var locId = await InsertLocationAsync("A-01");
            // GetActiveAssignmentsAsync Include 的 Schedule→Product→Part/Model 為必要關聯（INNER JOIN），
            // 需 seed 完整品項鏈，佔用列才會被查回（production 由 FK 保證此鏈必存在）
            var sid1 = await InsertScheduleAsync("P001", "M001");
            var sid2 = await InsertScheduleAsync("P002", "M002");
            var repository = CreateRepository();
            await repository.AssignAsync(locId, sid1, assignedBy: 1);
            await repository.AssignAsync(locId, sid2, assignedBy: 1);
            await repository.ReleaseAsync(sid1, releasedBy: 2);

            var active = await repository.GetActiveAssignmentsAsync(locId);

            Assert.Single(active);
            Assert.Equal(sid2, active[0].ScheduleId);
            Assert.NotNull(active[0].Schedule);            // Include 生效
            Assert.NotNull(active[0].Schedule!.Product);
        }

        // ─── filtered unique index 實證（同 schedule 只允許一筆現役） ─────────────
        // 本測試同時驗證 HasFilter("[released_at] IS NULL") 於 SQLite EnsureCreated() 是否真的建出 partial index。
        // 若 SQLite 未套用該索引，第二筆 insert 不會拋例外，本測試會失敗——即為執行緒面向退路的判定點。

        [Fact]
        public async Task AssignAsync_DuplicateActiveSchedule_ThrowsDbUpdate()
        {
            var locId = await InsertLocationAsync("A-01");
            var repository = CreateRepository();
            await repository.AssignAsync(locId, scheduleId: 100, assignedBy: 1);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => repository.AssignAsync(locId, scheduleId: 100, assignedBy: 1));
        }

        [Fact]
        public async Task AssignAsync_AfterRelease_CanReassignSameSchedule()
        {
            var locId = await InsertLocationAsync("A-01");
            var repository = CreateRepository();
            await repository.AssignAsync(locId, scheduleId: 100, assignedBy: 1);
            await repository.ReleaseAsync(scheduleId: 100, releasedBy: 2);

            // 下架後（released_at 非 NULL 不受 filtered index 約束）可再次上架同一 schedule
            var newId = await repository.AssignAsync(locId, scheduleId: 100, assignedBy: 1);

            Assert.True(newId > 0);
            Assert.True(await repository.HasActiveAssignmentAsync(100));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private WarehouseRepository CreateRepository() => new(CreateFactory());

        private IDbContextFactory<MesDbContext> CreateFactory()
        {
            var mock = new Mock<IDbContextFactory<MesDbContext>>();
            mock.Setup(f => f.CreateDbContext())
                .Returns(() => new MesDbContext(_options));
            mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MesDbContext(_options));
            return mock.Object;
        }

        private async Task<int> InsertLocationAsync(string code)
        {
            using var ctx = new MesDbContext(_options);
            var loc = new StorageLocation
            {
                Code         = code,
                LocationType = LocationType.Buffer,
                IsEnabled    = true,
                CreateAt     = DateTime.Today, // 明確賦值，避開 SQLite 無 sysdatetime() 預設
                UpdateAt     = DateTime.Today
            };
            ctx.StorageLocations.Add(loc);
            await ctx.SaveChangesAsync();
            return loc.LocationId;
        }

        // seed Part→Model→Product→Schedule 完整鏈，回傳 scheduleId（稽核欄位明確賦值，避開 SQLite 無 GETDATE() 預設）
        private async Task<int> InsertScheduleAsync(string partNo, string modelName)
        {
            using var ctx = new MesDbContext(_options);
            var part = new ProductPart { PartNo = partNo, CreateAt = DateTime.Today, UpdateAt = DateTime.Today };
            var model = new ProductModel { Name = modelName, CreateAt = DateTime.Today, UpdateAt = DateTime.Today };
            ctx.ProductParts.Add(part);
            ctx.Set<ProductModel>().Add(model);
            await ctx.SaveChangesAsync();

            var product = new Product { PartId = part.PartId, ModelId = model.ModelId, CreateAt = DateTime.Today, UpdateAt = DateTime.Today };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();

            var schedule = new Schedule
            {
                ProductId  = product.ProductId,
                ProcessId  = 1,
                Quantity   = 10,
                Status     = ScheduleStatus.Pending,
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
