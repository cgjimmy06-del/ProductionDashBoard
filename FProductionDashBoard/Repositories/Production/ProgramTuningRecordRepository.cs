using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class ProgramTuningRecordRepository : Repository<ProgramTuningRecord, MesDbContext>, IProgramTuningRecordRepository
    {
        public ProgramTuningRecordRepository(IDbContextFactory<MesDbContext> factory) : base(factory) { }

        public async Task<int> StartAsync(int equipmentId, int equipmentProductId, TuningType type, int startedBy, int managedBy, DateTime startedAt, bool forceStatus)
        {
            await using var ctx = _factory.CreateDbContext();

            // 強制安排：同一交易先把對應 equipment_product 狀態改為目標調試類型
            if (forceStatus)
            {
                var ep = await ctx.EquipmentProducts.FindAsync(equipmentProductId).ConfigureAwait(false);
                if (ep != null)
                {
                    ep.ProductionStatus = type;
                    ep.UpdateAt = DateTime.Now;
                }
            }

            var record = new ProgramTuningRecord
            {
                EquipmentId = equipmentId,
                EquipmentProductId = equipmentProductId,
                TuningType = type,
                Status = ProgramTuningStatus.InProgress,
                StartedBy = startedBy,
                ManagedBy = managedBy,
                StartedAt = startedAt,
                CreateAt = DateTime.Now,
                UpdateAt = DateTime.Now
            };
            ctx.ProgramTuningRecords.Add(record);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return record.ProgramTuningId;
        }

        public async Task EndAsync(int programTuningId, DateTime endedAt, string? description = null)
        {
            await using var ctx = _factory.CreateDbContext();
            var record = await ctx.ProgramTuningRecords.FindAsync(programTuningId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[EndAsync] 找不到調試紀錄 ProgramTuningId={programTuningId}");
            if (record.Status != ProgramTuningStatus.InProgress)
                throw new InvalidOperationException($"[EndAsync] 狀態不允許：{record.Status}");

            record.Status = ProgramTuningStatus.Completed;
            record.EndedAt = endedAt;
            record.Description = description;
            record.UpdateAt = DateTime.Now;

            var ep = await ctx.EquipmentProducts.FindAsync(record.EquipmentProductId).ConfigureAwait(false);
            if (ep != null)
            {
                ep.ProductionStatus = TuningType.Pending;
                ep.UpdateAt = DateTime.Now;
            }

            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<List<ProgramTuningRecord>> GetAllInProgressAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.ProgramTuningRecords
                .Where(r => r.Status == ProgramTuningStatus.InProgress)
                .Include(r => r.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Part)
                .Include(r => r.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Model)
                .Include(r => r.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Process)
                .Include(r => r.StartedByEmployee)
                .Include(r => r.ManagedByEmployee)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<ProgramTuningRecord?> GetInProgressByEquipmentAsync(int equipmentId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.ProgramTuningRecords
                .Where(r => r.EquipmentId == equipmentId && r.Status == ProgramTuningStatus.InProgress)
                .Include(r => r.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Part)
                .Include(r => r.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Model)
                .Include(r => r.EquipmentProduct)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Process)
                .Include(r => r.StartedByEmployee)
                .Include(r => r.ManagedByEmployee)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public async Task<Dictionary<int, string>> GetLastCompletedTeachingNamesByEquipmentAsync(int equipmentId)
        {
            await using var ctx = _factory.CreateDbContext();
            // 每個 equipment_product 取最近一筆（EndedAt 為主、缺則 StartedAt）的執行人姓名；
            // DB 端分組投影，避免歷史紀錄隨時間累積後全量載入
            var rows = await ctx.ProgramTuningRecords
                .Where(r => r.EquipmentId == equipmentId
                         && r.Status == ProgramTuningStatus.Completed
                         && r.TuningType == TuningType.Teaching)
                .GroupBy(r => r.EquipmentProductId)
                .Select(g => new
                {
                    g.Key,
                    Name = g.OrderByDescending(r => r.EndedAt ?? r.StartedAt)
                            .Select(r => r.StartedByEmployee!.Name)
                            .First()
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.ToDictionary(x => x.Key, x => x.Name);
        }
    }
}
