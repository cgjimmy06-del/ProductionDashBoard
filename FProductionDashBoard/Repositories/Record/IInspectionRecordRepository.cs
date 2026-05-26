using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IInspectionRecordRepository : IRepository<InspectionRecord, MesDbContext>
    {
        /// <summary>
        /// 新增一筆檢驗紀錄
        /// </summary>
        public Task<int> AddInspectionRecordAsync(InspectionType type, int equipmentId, int employeeId,
            bool result, int? timeSlotId, int? productId, string? errorCode, string? description,
            DateTime? operatedAt = null);
        /// <summary>
        /// 檢查某設備在指定時段是否已有巡檢紀錄
        /// </summary>
        public Task<bool> ExistsInspectionInSlotAsync(int equipmentId, int timeSlotId, DateTime date);
        /// <summary>
        /// 檢查某設備在每個時段的狀態 (hasRecord, result)
        /// </summary>
        public Task<List<(bool hasRecord, bool result)>> GetStatusForAllSlotsAsync(int equipmentId, DateTime businessDate);

    }
}
