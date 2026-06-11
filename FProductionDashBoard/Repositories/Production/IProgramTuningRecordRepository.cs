using FProductionDashBoard.Models;
using System;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IProgramTuningRecordRepository : IRepository<ProgramTuningRecord, MesDbContext>
    {
        Task<int> StartAsync(int equipmentId, int equipmentProductId, TuningType type, int startedBy, DateTime startedAt);
        // 結束調試並將對應 equipment_product 狀態轉為 Pending（同一交易）
        Task EndAsync(int programTuningId, DateTime endedAt, string? description = null);
        Task<ProgramTuningRecord?> GetInProgressByEquipmentAsync(int equipmentId);
        Task<List<ProgramTuningRecord>> GetAllInProgressAsync();
    }
}
