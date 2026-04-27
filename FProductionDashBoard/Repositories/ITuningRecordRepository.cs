using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface ITuningRecordRepository : IRepository<TuningRecord, MesDbContext>
    {
        public Task<int> AddTuningRecordAsync(TuningType type, int equipmentId, int employeeId,
            int durationSec, string? productName, DateTime? operatedAt = null);
    }
}
