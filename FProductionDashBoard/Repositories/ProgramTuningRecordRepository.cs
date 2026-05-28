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

        public async Task<int> StartAsync(int equipmentId, int equipmentProductId, TuningType type, int startedBy, DateTime startedAt)
        {
            await using var ctx = _factory.CreateDbContext();
            var record = new ProgramTuningRecord
            {
                EquipmentId = equipmentId,
                EquipmentProductId = equipmentProductId,
                TuningType = type,
                Status = ProgramTuningStatus.InProgress,
                StartedBy = startedBy,
                StartedAt = startedAt,
                CreateAt = DateTime.Now,
                UpdateAt = DateTime.Now
            };
            ctx.ProgramTuningRecords.Add(record);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return record.ProgramTuningId;
        }

        public async Task<int> EndAsync(int programTuningId, DateTime endedAt, string? description = null)
        {
            await using var ctx = _factory.CreateDbContext();
            var record = await ctx.ProgramTuningRecords.FindAsync(programTuningId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[EndAsync] ProgramTuningId {programTuningId} 不存在");
            if (record.Status != ProgramTuningStatus.InProgress)
                throw new InvalidOperationException($"[EndAsync] 狀態不允許：{record.Status}");
            record.Status = ProgramTuningStatus.Completed;
            record.EndedAt = endedAt;
            record.Description = description;
            record.UpdateAt = DateTime.Now;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return record.EquipmentProductId;
        }

        public async Task<ProgramTuningRecord?> GetInProgressByEquipmentAsync(int equipmentId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.ProgramTuningRecords
                .Where(r => r.EquipmentId == equipmentId && r.Status == ProgramTuningStatus.InProgress)
                .Include(r => r.EquipmentProductNav)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Part)
                .Include(r => r.EquipmentProductNav)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Product)
                    .ThenInclude(p => p!.Model)
                .Include(r => r.EquipmentProductNav)
                    .ThenInclude(ep => ep!.Sop)
                    .ThenInclude(s => s!.Process)
                .Include(r => r.StartedByEmployee)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }
    }
}
