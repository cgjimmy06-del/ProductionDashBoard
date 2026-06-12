using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class OrderProductionRepository : Repository<OrderProduction, MesDbContext>, IOrderProductionRepository
    {
        public OrderProductionRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<List<OrderProduction>> GetAllWithDetailsAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.OrderProductions
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Part)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Model)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Process)
                .Include(o => o.StartedByEmployee)
                .OrderBy(o => o.CreateAt)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<OrderProduction>> GetByEquipmentAsync(int equipmentId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.OrderProductions
                .Where(o => o.EquipmentId == equipmentId
                    && (o.Status == OrderProductionStatus.Pending
                        || o.Status == OrderProductionStatus.InProduction))
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Part)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Model)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Process)
                .Include(o => o.StartedByEmployee)
                .OrderBy(o => o.CreateAt)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<OrderProduction>> GetByScheduleAsync(int scheduleId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.OrderProductions
                .Where(o => o.ScheduleId == scheduleId)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Part)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Model)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Process)
                .Include(o => o.StartedByEmployee)
                .OrderBy(o => o.CreateAt)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<OrderProduction?> GetInProductionByEquipmentAsync(int equipmentId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.OrderProductions
                .Where(o => o.EquipmentId == equipmentId && o.Status == OrderProductionStatus.InProduction)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Part)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Model)
                .Include(o => o.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Process)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public async Task<int> AddAsync(int equipmentId, int equipmentProductId, int? quantity, int createdBy, int? scheduleId = null)
        {
            await using var ctx = _factory.CreateDbContext();
            var order = new OrderProduction
            {
                EquipmentId = equipmentId,
                EquipmentProductId = equipmentProductId,
                ScheduleId = scheduleId,
                Status = OrderProductionStatus.Pending,
                Quantity = quantity,
                CreatedBy = createdBy,
                CreateAt = DateTime.Now,
                UpdateAt = DateTime.Now
            };
            ctx.OrderProductions.Add(order);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return order.OrderId;
        }

        public async Task StartAsync(int orderId, int startedBy, DateTime startedAt)
        {
            await using var ctx = _factory.CreateDbContext();
            var order = await ctx.OrderProductions.FindAsync(orderId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[StartAsync] 找不到接單 OrderId={orderId}");
            if (order.Status != OrderProductionStatus.Pending)
                throw new InvalidOperationException($"[StartAsync] 狀態不允許：{order.Status}");
            order.Status = OrderProductionStatus.InProduction;
            order.StartedBy = startedBy;
            order.StartedAt = startedAt;
            order.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task EndAsync(int orderId, DateTime endedAt)
        {
            await using var ctx = _factory.CreateDbContext();
            var order = await ctx.OrderProductions.FindAsync(orderId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[EndAsync] 找不到接單 OrderId={orderId}");
            if (order.Status != OrderProductionStatus.InProduction)
                throw new InvalidOperationException($"[EndAsync] 狀態不允許：{order.Status}");
            order.Status = OrderProductionStatus.Completed;
            order.EndedAt = endedAt;
            order.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task CancelAsync(int orderId, string? description)
        {
            await using var ctx = _factory.CreateDbContext();
            var order = await ctx.OrderProductions.FindAsync(orderId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[CancelAsync] 找不到接單 OrderId={orderId}");
            if (order.Status != OrderProductionStatus.Pending && order.Status != OrderProductionStatus.InProduction)
                throw new InvalidOperationException($"[CancelAsync] 狀態不允許：{order.Status}");
            order.Status = OrderProductionStatus.Cancelled;
            order.Description = description;
            order.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
