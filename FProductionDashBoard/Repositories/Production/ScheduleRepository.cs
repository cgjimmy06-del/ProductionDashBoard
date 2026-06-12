using FProductionDashBoard.Models;
using FProductionDashBoard.Services.Exceptions;
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
                throw new BusinessRuleException($"[MarkScheduledAsync] 狀態不允許：{schedule.Status}");
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
                throw new BusinessRuleException($"[MarkVerifiedAsync] 狀態不允許：{schedule.Status}");
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
                throw new BusinessRuleException($"[MarkReleasedAsync] 狀態不允許：{schedule.Status}");
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
                throw new BusinessRuleException($"[MarkCancelledAsync] 狀態不允許：{schedule.Status}");
            schedule.Status = ScheduleStatus.Cancelled;
            if (description != null) schedule.Description = description;
            schedule.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task ForceCompleteAsync(int scheduleId, int verifiedBy, DateTime verifiedAt,
            int? actualQuantity, string description)
        {
            await using var ctx = _factory.CreateDbContext();
            var schedule = await ctx.Schedules.FindAsync(scheduleId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[ForceCompleteAsync] 找不到排程 ScheduleId={scheduleId}");
            if (schedule.Status != ScheduleStatus.Pending)
                throw new BusinessRuleException($"[ForceCompleteAsync] 狀態不允許：{schedule.Status}（需為 Pending）");

            schedule.Status         = ScheduleStatus.Completed;
            schedule.VerifiedBy     = verifiedBy;
            schedule.VerifiedAt     = verifiedAt;
            schedule.ActualQuantity = actualQuantity ?? schedule.Quantity;
            schedule.Description    = description;
            schedule.UpdateAt       = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task SplitScheduleAsync(int originalId, int remainingQuantity, int releasedBy,
            DateTime releasedAt, string? description)
        {
            await using var ctx = _factory.CreateDbContext();
            var original = await ctx.Schedules.FindAsync(originalId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[SplitScheduleAsync] 找不到原排程 ScheduleId={originalId}");
            if (original.Status != ScheduleStatus.Completed)
                throw new BusinessRuleException($"[SplitScheduleAsync] 原排程狀態不允許拆單：{original.Status}（需為 Completed）");

            var completedQty = original.ActualQuantity ?? original.Quantity;
            if (completedQty + remainingQuantity != original.Quantity)
                throw new BusinessRuleException(
                    $"[SplitScheduleAsync] actual_quantity({completedQty}) + remainingQuantity({remainingQuantity}) " +
                    $"必須等於原始數量({original.Quantity})");

            var splitNote = description ?? $"拆單：完成 {completedQty}，剩餘 {remainingQuantity}";
            original.Status      = ScheduleStatus.Released;
            original.ReleasedBy  = releasedBy;
            original.ReleasedAt  = releasedAt;
            original.Description = splitNote;
            original.UpdateAt    = releasedAt;

            ctx.Schedules.Add(new Schedule
            {
                ProductId   = original.ProductId,
                ProcessId   = original.ProcessId,
                Quantity    = remainingQuantity,
                LotNo       = original.LotNo,
                Status      = ScheduleStatus.Pending,
                ReceivedBy  = releasedBy,
                ReceivedAt  = releasedAt,
                ParentId    = original.ScheduleId,
                Description = splitNote,
                CreateAt    = releasedAt,
                UpdateAt    = releasedAt
            });
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task RecalcActualQuantityAsync(int scheduleId)
        {
            await using var ctx = _factory.CreateDbContext();
            var schedule = await ctx.Schedules.FindAsync(scheduleId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[RecalcActualQuantityAsync] 找不到排程 ScheduleId={scheduleId}");
            var totalQty = await ctx.OrderProductions
                .Where(o => o.ScheduleId == scheduleId && o.Status != OrderProductionStatus.Cancelled)
                .SumAsync(o => o.Quantity ?? 0)
                .ConfigureAwait(false);
            schedule.ActualQuantity = totalQty;
            schedule.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task MarkScheduledAndRecalcAsync(int scheduleId, int userId, DateTime now)
        {
            await using var ctx = _factory.CreateDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync().ConfigureAwait(false);

            var schedule = await ctx.Schedules.FindAsync(scheduleId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[MarkScheduledAndRecalcAsync] 找不到排程 ScheduleId={scheduleId}");

            if (schedule.Status == ScheduleStatus.Pending)
            {
                schedule.Status      = ScheduleStatus.Scheduled;
                schedule.ScheduledBy = userId;
                schedule.ScheduledAt = now;
            }

            var totalQty = await ctx.OrderProductions
                .Where(o => o.ScheduleId == scheduleId && o.Status != OrderProductionStatus.Cancelled)
                .SumAsync(o => o.Quantity ?? 0)
                .ConfigureAwait(false);
            schedule.ActualQuantity = totalQty;
            schedule.UpdateAt = now;

            await ctx.SaveChangesAsync().ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
        }
    }
}
