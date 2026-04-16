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
    public class TimeSlotLookupRepository : Repository<TimeSlotLookup, MesDbContext>, ITimeSlotLookupRepository
    {
        public TimeSlotLookupRepository(MesDbContext context) : base(context)
        {
        }
        public async Task<int?> GetCurrentTimeSlotIdAsync(DateTime businessDate)
        {
            var slots = await _context.TimeSlotLookups.ToListAsync();

            var now = DateTime.Now;
            foreach (var slot in slots)
            {
                var slotStart = businessDate.Date.Add(slot.StartAt);
                var slotEnd = businessDate.Date.Add(slot.EndAt);

                // 跨日邏輯: 跨整點 EndAt + 1；跨日 StartAt, EndAt + 1
                if (slot.IsCrossDay)
                {
                    slotEnd = slotEnd.AddDays(1);
                    if (slot.EndAt > slot.StartAt)
                        slotStart = slotStart.AddDays(1);
                }

                if (now >= slotStart && now < slotEnd)
                    return slot.TimeSlotId; // 找到當前時段
            }
            return null; // 若不在任何時段範圍內
        }
    }
    public class InspectionRecordRepository : Repository<InspectionRecord, MesDbContext>, IInspectionRecordRepository
    {
        public InspectionRecordRepository(MesDbContext context) : base(context)
        {
        }
        
        public async Task<int> AddInspectionRecordAsync(InspectionType type, int equipmentId, int employeeId,
            bool result, int? timeSlotId, string? productName, string? errorCode, string? description)
        {
            var record = new InspectionRecord
            {
                InspectionType = type,
                EquipmentId = equipmentId,
                EmployeeId = employeeId,
                TimeSlotId = timeSlotId,
                Product = productName,
                Result = result,
                ErrorCode = errorCode,
                Description = description,
                CreateAt = DateTime.Now
            };

            _context.InspectionRecords.Add(record);
            await _context.SaveChangesAsync();
            return record.InspectionId;
        }
        public async Task<bool> ExistsInspectionInSlotAsync(int equipmentId, int timeSlotId, DateTime businessDate)
        {
            var businessDateEnd = businessDate.AddDays(1);

            return await _context.InspectionRecords
                .AnyAsync(r => r.EquipmentId == equipmentId &&
                               r.TimeSlotId == timeSlotId &&
                               r.CreateAt >= businessDate &&
                               r.CreateAt < businessDateEnd);
        }
        public async Task<List<(bool hasRecord, bool result)>> GetStatusForAllSlotsAsync(int equipmentId, DateTime businessDate)
        {
            var businessDateEnd = businessDate.AddDays(1);

            // 先抓出當日該設備的所有紀錄
            var records = await _context.InspectionRecords
                .Where(r => r.EquipmentId == equipmentId &&
                            r.CreateAt >= businessDate &&
                            r.CreateAt < businessDateEnd).ToListAsync();

            // 抓出所有時段定義
            var slots = await _context.TimeSlotLookups.OrderBy(s => s.TimeSlotId).ToListAsync();

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
