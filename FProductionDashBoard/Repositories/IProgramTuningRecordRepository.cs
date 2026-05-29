using FProductionDashBoard.Models;
using System;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IProgramTuningRecordRepository : IRepository<ProgramTuningRecord, MesDbContext>
    {
        Task<int> StartAsync(int equipmentId, int equipmentProductId, TuningType type, int startedBy, DateTime startedAt);
        // Returns EquipmentProductId of the ended record
        Task<int> EndAsync(int programTuningId, DateTime endedAt, string? description = null);
        Task<ProgramTuningRecord?> GetInProgressByEquipmentAsync(int equipmentId);
    }
}
