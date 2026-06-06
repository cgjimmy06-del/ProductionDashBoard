using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class ScheduleRepository : Repository<Schedule, MesDbContext>, IScheduleRepository
    {
        public ScheduleRepository(IDbContextFactory<MesDbContext> factory) : base(factory) { }

        public async Task<List<Schedule>> GetAllWithDetailsAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Schedules
                .Include(s => s.Product).ThenInclude(p => p!.Part)
                .Include(s => s.Product).ThenInclude(p => p!.Model)
                .Include(s => s.Process)
                .Include(s => s.ReceivedByEmployee)
                .Include(s => s.ScheduledByEmployee)
                .Include(s => s.VerifiedByEmployee)
                .Include(s => s.ReleasedByEmployee)
                .OrderByDescending(s => s.CreateAt)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<Schedule?> GetByIdWithDetailsAsync(int scheduleId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Schedules
                .Include(s => s.Product).ThenInclude(p => p!.Part)
                .Include(s => s.Product).ThenInclude(p => p!.Model)
                .Include(s => s.Process)
                .Include(s => s.ReceivedByEmployee)
                .Include(s => s.ScheduledByEmployee)
                .Include(s => s.VerifiedByEmployee)
                .Include(s => s.ReleasedByEmployee)
                .Include(s => s.Children)
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId)
                .ConfigureAwait(false);
        }

        public async Task<List<Schedule>> GetByStatusAsync(ScheduleStatus status)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Schedules
                .Where(s => s.Status == status)
                .Include(s => s.Product).ThenInclude(p => p!.Part)
                .Include(s => s.Product).ThenInclude(p => p!.Model)
                .Include(s => s.Process)
                .OrderByDescending(s => s.CreateAt)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<Schedule>> GetChildrenAsync(int parentId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Schedules
                .Where(s => s.ParentId == parentId)
                .OrderBy(s => s.CreateAt)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public new async Task<int> AddAsync(Schedule entity)
        {
            await using var ctx = _factory.CreateDbContext();
            ctx.Schedules.Add(entity);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return entity.ScheduleId;
        }

        public async Task MarkScheduledAsync(int scheduleId, int scheduledBy, DateTime scheduledAt)
        {
            await using var ctx = _factory.CreateDbContext();
            var schedule = await ctx.Schedules.FindAsync(scheduleId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[MarkScheduledAsync] 找不到排程 ScheduleId={scheduleId}");
            if (schedule.Status != ScheduleStatus.Pending)
                throw new InvalidOperationException($"[MarkScheduledAsync] 狀態不允許：{schedule.Status}");
            schedule.Status = ScheduleStatus.Scheduled;
            schedule.ScheduledBy = scheduledBy;
            schedule.ScheduledAt = scheduledAt;
            schedule.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task MarkVerifiedAsync(int scheduleId, int verifiedBy, DateTime verifiedAt,
            int? actualQuantity, string? description)
        {
            await using var ctx = _factory.CreateDbContext();
            var schedule = await ctx.Schedules.FindAsync(scheduleId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[MarkVerifiedAsync] 找不到排程 ScheduleId={scheduleId}");
            if (schedule.Status != ScheduleStatus.Scheduled)
                throw new InvalidOperationException($"[MarkVerifiedAsync] 狀態不允許：{schedule.Status}");
            schedule.Status = ScheduleStatus.Completed;
            schedule.VerifiedBy = verifiedBy;
            schedule.VerifiedAt = verifiedAt;
            schedule.ActualQuantity = actualQuantity ?? schedule.Quantity;
            if (description != null) schedule.Description = description;
            schedule.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task MarkReleasedAsync(int scheduleId, int releasedBy, DateTime releasedAt, string? description)
        {
            await using var ctx = _factory.CreateDbContext();
            var schedule = await ctx.Schedules.FindAsync(scheduleId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[MarkReleasedAsync] 找不到排程 ScheduleId={scheduleId}");
            if (schedule.Status != ScheduleStatus.Completed)
                throw new InvalidOperationException($"[MarkReleasedAsync] 狀態不允許：{schedule.Status}");
            schedule.Status = ScheduleStatus.Released;
            schedule.ReleasedBy = releasedBy;
            schedule.ReleasedAt = releasedAt;
            if (description != null) schedule.Description = description;
            schedule.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task MarkCancelledAsync(int scheduleId, string? description)
        {
            await using var ctx = _factory.CreateDbContext();
            var schedule = await ctx.Schedules.FindAsync(scheduleId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[MarkCancelledAsync] 找不到排程 ScheduleId={scheduleId}");
            if (schedule.Status == ScheduleStatus.Released || schedule.Status == ScheduleStatus.Cancelled)
                throw new InvalidOperationException($"[MarkCancelledAsync] 狀態不允許：{schedule.Status}");
            schedule.Status = ScheduleStatus.Cancelled;
            if (description != null) schedule.Description = description;
            schedule.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
