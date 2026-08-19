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
