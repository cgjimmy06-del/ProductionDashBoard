using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class WarehouseRepository : Repository<StorageLocation, MesDbContext>, IWarehouseRepository
    {
        public WarehouseRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<bool> ExistsLocationCodeAsync(string code, int? excludeLocationId = null)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.StorageLocations
                .AnyAsync(l => l.Code == code && (excludeLocationId == null || l.LocationId != excludeLocationId))
                .ConfigureAwait(false);
        }

        public async Task<bool> HasActiveAssignmentAsync(int scheduleId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.LocationAssignments
                .AnyAsync(a => a.ScheduleId == scheduleId && a.ReleasedAt == null)
                .ConfigureAwait(false);
        }

        public async Task<bool> HasActiveAssignmentsAtLocationAsync(int locationId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.LocationAssignments
                .AnyAsync(a => a.LocationId == locationId && a.ReleasedAt == null)
                .ConfigureAwait(false);
        }

        public async Task<int> AssignAsync(int locationId, int scheduleId, int assignedBy)
        {
            await using var ctx = _factory.CreateDbContext();
            var assignment = new LocationAssignment
            {
                LocationId = locationId,
                ScheduleId = scheduleId,
                AssignedBy = assignedBy,
                AssignedAt = DateTime.Now
            };
            ctx.LocationAssignments.Add(assignment);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return assignment.AssignmentId;
        }

        public async Task ReleaseAsync(int scheduleId, int releasedBy)
        {
            await using var ctx = _factory.CreateDbContext();
            var active = await ctx.LocationAssignments
                .FirstOrDefaultAsync(a => a.ScheduleId == scheduleId && a.ReleasedAt == null)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[ReleaseAsync] 找不到現役佔用 ScheduleId={scheduleId}");
            active.ReleasedBy = releasedBy;
            active.ReleasedAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task ReassignAsync(int scheduleId, int newLocationId, int operatorId)
        {
            await using var ctx = _factory.CreateDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync().ConfigureAwait(false);

            var now = DateTime.Now;
            // 先釋放現役佔用（若有），SaveChanges 後才 insert 新佔用；符合 filtered unique index（released_at IS NULL 唯一）的排序要求
            var active = await ctx.LocationAssignments
                .FirstOrDefaultAsync(a => a.ScheduleId == scheduleId && a.ReleasedAt == null)
                .ConfigureAwait(false);
            if (active != null)
            {
                active.ReleasedBy = operatorId;
                active.ReleasedAt = now;
                await ctx.SaveChangesAsync().ConfigureAwait(false);
            }

            // 無現役佔用時退化為單純上架
            ctx.LocationAssignments.Add(new LocationAssignment
            {
                LocationId = newLocationId,
                ScheduleId = scheduleId,
                AssignedBy = operatorId,
                AssignedAt = now
            });
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
        }

        public async Task<List<LocationAssignment>> GetActiveAssignmentsAsync(int locationId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.LocationAssignments
                .Where(a => a.LocationId == locationId && a.ReleasedAt == null)
                .Include(a => a.Schedule)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Part)
                .Include(a => a.Schedule)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Model)
                .OrderBy(a => a.AssignedAt)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<LocationAssignment>> GetAllActiveAssignmentsAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.LocationAssignments
                .Where(a => a.ReleasedAt == null)
                .Include(a => a.Location)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<Dictionary<int, int>> GetActiveCountByLocationAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.LocationAssignments
                .Where(a => a.ReleasedAt == null)
                .GroupBy(a => a.LocationId)
                .Select(g => new { LocationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.LocationId, x => x.Count)
                .ConfigureAwait(false);
        }
    }
}
