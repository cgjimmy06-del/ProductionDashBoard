using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class TuningRecordRepository : Repository<TuningRecord, MesDbContext>, ITuningRecordRepository
    {
        public TuningRecordRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<int> AddTuningRecordAsync(TuningType type, int equipmentId, int employeeId, 
            int durationSec, string? productName, DateTime? operatedAt = null)
        {
            await using var ctx = _factory.CreateDbContext();
            var record = new TuningRecord
            {
                TuningType = type,
                EquipmentId = equipmentId,
                EmployeeId = employeeId,
                DurationSec = durationSec,
                Product = productName,
                CreateAt = operatedAt ?? DateTime.Now
            };

            ctx.TuningRecords.Add(record);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return record.TuningId;
        }
    }


}
