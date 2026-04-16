using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface ITimeSlotLookupRepository : IRepository<TimeSlotLookup, MesDbContext>
    {
    }
    public interface IInspectionRecordRepository : IRepository<InspectionRecord, MesDbContext>
    {
        public Task<int> AddInspectionRecordAsync(InspectionType type, int equipmentId, int employeeId,
            bool result, int? timeSlotId, string? productName, string? abnormalReport);
        /// <summary>
        /// 檢查某設備在指定時段是否已有巡檢紀錄
        /// </summary>
        public Task<bool> ExistsInspectionInSlotAsync(int equipmentId, int timeSlotId, DateTime date);
        /// <summary>
        /// 檢查某設備在每個時段的狀態 (是否有紀錄、紀錄結果)
        /// </summary>
        public Task<List<(bool hasRecord, bool result)>> GetStatusForAllSlotsAsync(int equipmentId, DateTime businessDate);

        public Task<List<InspectionRecord>> GetRecordsByEquipmentAsync(int equipmentId);
        public Task<List<InspectionRecord>> GetRecordsByTimeSlotAsync(int timeSlotId, DateTime date);
    }
}
