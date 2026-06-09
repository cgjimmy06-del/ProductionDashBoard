using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class InspectionRecordRepository : Repository<InspectionRecord, MesDbContext>, IInspectionRecordRepository
    {
        public InspectionRecordRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<int> AddInspectionRecordAsync(InspectionType type, int equipmentId, int employeeId,
            bool result, int? timeSlotId, int? productId, string? errorCode, string? description,
            DateTime? operatedAt = null)
        {
            await using var ctx = _factory.CreateDbContext();
            var record = new InspectionRecord
            {
                InspectionType = type,
                EquipmentId = equipmentId,
                EmployeeId = employeeId,
                TimeSlotId = timeSlotId,
                ProductId = productId,
                Result = result,
                ErrorCode = errorCode,
                Description = description,
                CreateAt = operatedAt ?? DateTime.Now
            };

            ctx.InspectionRecords.Add(record);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return record.InspectionId;
        }

        public async Task<bool> ExistsInspectionInSlotAsync(int equipmentId, int timeSlotId, DateTime businessDate)
        {
            await using var ctx = _factory.CreateDbContext();
            var businessDateEnd = businessDate.AddDays(1);

            return await ctx.InspectionRecords
                .AnyAsync(r => r.EquipmentId == equipmentId &&
                               r.TimeSlotId == timeSlotId &&
                               r.CreateAt >= businessDate &&
                               r.CreateAt < businessDateEnd).ConfigureAwait(false);
        }

        public async Task<List<(bool hasRecord, bool result)>> GetStatusForAllSlotsAsync(int equipmentId, DateTime businessDate)
        {
            await using var ctx = _factory.CreateDbContext();
            var businessDateEnd = businessDate.AddDays(1);

            var records = await ctx.InspectionRecords
                .Where(r => r.EquipmentId == equipmentId &&
                            r.CreateAt >= businessDate &&
                            r.CreateAt < businessDateEnd).ToListAsync().ConfigureAwait(false);

            var slots = await ctx.TimeSlotLookups.OrderBy(s => s.TimeSlotId).ToListAsync().ConfigureAwait(false);

            var result = new List<(bool hasRecord, bool result)>();
            foreach (var slot in slots)
            {
                var record = records.FirstOrDefault(r => r.TimeSlotId == slot.TimeSlotId);

                if (record == null)
                    result.Add((false, false));
                else
                    result.Add((true, record.Result));
            }

            return result;
        }

        /// <summary>
        /// 查詢某設備的所有檢驗紀錄
        /// </summary>
        public async Task<List<InspectionRecord>> GetRecordsByEquipmentAsync(int equipmentId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.InspectionRecords
                .Where(r => r.EquipmentId == equipmentId)
                .Include(r => r.Employee)
                .Include(r => r.TimeSlot)
                .Include(r => r.Error)
                .OrderByDescending(r => r.CreateAt)
                .ToListAsync().ConfigureAwait(false);
        }
    }
}
