using FProductionDashBoard.Models;
using System;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IProgramTuningRecordRepository : IRepository<ProgramTuningRecord, MesDbContext>
    {
        // forceStatus=true 時於同一交易先將對應 equipment_product 狀態改為 type（強制安排）
        Task<int> StartAsync(int equipmentId, int equipmentProductId, TuningType type, int startedBy, int managedBy, DateTime startedAt, bool forceStatus);
        // 結束調試並將對應 equipment_product 狀態轉為 Pending（同一交易）
        Task EndAsync(int programTuningId, DateTime endedAt, string? description = null);
        Task<ProgramTuningRecord?> GetInProgressByEquipmentAsync(int equipmentId);
        Task<List<ProgramTuningRecord>> GetAllInProgressAsync();
        // 同設備每個 equipment_product 最近一筆已完成帶點(Teaching)的執行人姓名
        Task<Dictionary<int, string>> GetLastCompletedTeachingNamesByEquipmentAsync(int equipmentId);
    }
}
