using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class TimeSlotLookupRepository : Repository<TimeSlotLookup, MesDbContext>, ITimeSlotLookupRepository
    {
        public TimeSlotLookupRepository(MesDbContext context) : base(context)
        {
        }
    }
    public class InspectionRecordRepository : Repository<InspectionRecord, MesDbContext>, IInspectionRecordRepository
    {
        public InspectionRecordRepository(MesDbContext context) : base(context)
        {
        }
        /// <summary>
        /// 新增一筆檢驗紀錄
        /// </summary>
        public async Task<int> AddInspectionRecordAsync(InspectionType type, int equipmentId, int employeeId,
            bool result, int? timeSlotId, string? productName, string? abnormalReport)
        {
            var record = new InspectionRecord
            {
                InspectionType = type,
                EquipmentId = equipmentId,
                EmployeeId = employeeId,
                TimeSlotId = timeSlotId,
                Product = productName,
                Result = result,
                ErrorCode = abnormalReport,
                CreateAt = DateTime.Now
            };

            _context.InspectionRecords.Add(record);
            await _context.SaveChangesAsync();
            return record.InspectionId;
        }
        /// <summary>
        /// 檢查某設備在指定時段是否已有巡檢紀錄
        /// </summary>
        public async Task<bool> ExistsInspectionInSlotAsync(int equipmentId, int timeSlotId, DateTime businessDate)
        {
            var businessDateEnd = businessDate.AddDays(1);

            return await _context.InspectionRecords
                .AnyAsync(r => r.EquipmentId == equipmentId &&
                               r.TimeSlotId == timeSlotId &&
                               r.CreateAt >= businessDate &&
                               r.CreateAt < businessDateEnd);
        }
        /// <summary>
        /// 檢查某設備在每個時段的狀態 (hasRecord、result)
        /// </summary>
        public async Task<List<(bool hasRecord, bool result)>> GetStatusForAllSlotsAsync(int equipmentId, DateTime businessDate)
        {
            var businessDateEnd = businessDate.AddDays(1);

            // 先抓出當日該設備的所有紀錄
            var records = await _context.InspectionRecords
                .Where(r => r.EquipmentId == equipmentId &&
                            r.CreateAt >= businessDate &&
                            r.CreateAt < businessDateEnd).ToListAsync();

            // 抓出所有時段定義
            var slots = await _context.TimeSlotLookups
                .OrderBy(s => s.StartAt).ToListAsync();

            var result = new List<(bool hasRecord, bool result)>();

            foreach (var slot in slots)
            {
                var record = records.FirstOrDefault(r => r.TimeSlotId == slot.TimeSlotId);

                if (record == null)
                    result.Add((false, false)); // 沒有紀錄 → result 固定 false
                else
                    result.Add((true, record.Result)); // 有紀錄 → result 取紀錄的結果
            }

            return result;
        }



        /// <summary>
        /// 查詢某設備的所有檢驗紀錄
        /// </summary>
        public async Task<List<InspectionRecord>> GetRecordsByEquipmentAsync(int equipmentId)
        {
            return await _context.InspectionRecords
                .Where(r => r.EquipmentId == equipmentId)
                .Include(r => r.Employee)
                .Include(r => r.TimeSlot)
                .Include(r => r.Error)
                .OrderByDescending(r => r.CreateAt)
                .ToListAsync();
        }

        /// <summary>
        /// 查詢某時段的巡檢紀錄
        /// </summary>
        public async Task<List<InspectionRecord>> GetRecordsByTimeSlotAsync(int timeSlotId, DateTime date)
        {
            return await _context.InspectionRecords
                .Where(r => r.TimeSlotId == timeSlotId && r.CreateAt == date.Date)
                .Include(r => r.Equipment)
                .Include(r => r.Employee)
                .ToListAsync();
        }

    }

}
