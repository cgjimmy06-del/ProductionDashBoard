using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Exceptions;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services.Offline.Payloads;
using FProductionDashBoard.UiModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Reflection.Metadata.BlobBuilder;

namespace FProductionDashBoard.Services.V1
{
    public class DataService : IDataService
    {
        public DateTime BusinessDay { get; set; }
        public IEquipmentRepository EquipmentRep { get; }
        public IEmployeeRepository EmployeeRep { get; }
        public IMaterialRepository MaterialRep { get; }
        public IErrorListRepository ErrorListRep { get; }
        public IMaterialReplacementRepository MaterialReplacementRep { get; }
        public ITimeSlotLookupRepository TimeSlotLookupRep { get; }
        public IInspectionRecordRepository InspectionRecordRep { get; }


        private readonly IOfflineCacheService _offlineCache;

        public DataService(IEquipmentRepository equipmentrep, IEmployeeRepository workerrep, IMaterialRepository materialrep,
            IErrorListRepository errorListRep, IMaterialReplacementRepository materialReplacementRep,
            IInspectionRecordRepository inspectionRecordRep, ITimeSlotLookupRepository timeSlotLookupRep,
            IOfflineCacheService offlineCache)
        {
            EquipmentRep = equipmentrep;
            EmployeeRep = workerrep;
            MaterialRep = materialrep;
            ErrorListRep = errorListRep;
            MaterialReplacementRep = materialReplacementRep;
            InspectionRecordRep = inspectionRecordRep;
            TimeSlotLookupRep = timeSlotLookupRep;
            _offlineCache = offlineCache;
        }


        #region 清單查詢與 Mapping
        public async Task<List<DeviceInfo>> GetDevicesAsync()
        {
            if (!EquipmentRep.CheckConnection())
                throw new InvalidOperationException("Equipment repository connection failed");
            var list = await EquipmentRep.GetAllAsync();
            return list.Select(eq => new DeviceInfo
            {
                Id = eq.Id,
                DeviceID = eq.Code,
                Name = eq.Name,
                IP = eq.Ip,
                Port = eq.Port,
                TypeId = eq.TypeId,
                Factory = eq.Factory,
                Building = eq.Building,
                Floor = eq.Floor,
                Description = eq.Description
            }).ToList();
        }
        public async Task<List<UserInfo>> GetUsersAsync()
        {
            if (!EmployeeRep.CheckConnection())
                throw new InvalidOperationException("Employee repository connection failed");
            var list = await EmployeeRep.GetAllAsync();
            return list.Select(us => new UserInfo
            {
                Id = us.EmployeeId,
                UserId = us.UserId,
                Name = us.Name,
                CardId = us.CardId,
                Password = us.Password,
                Email = us.Email,
                RoleId = us.RoleId,
                DepartmentId = us.DepartmentId
            }).ToList();
        }
        public async Task<List<MaterialInfo>> GetMaterialsAsync()
        {
            if (!MaterialRep.CheckConnection())
                throw new InvalidOperationException("Material repository connection failed");
            var list = await MaterialRep.GetAllAsync();
            return list.Select(ma => new MaterialInfo
            {
                Id = ma.MaterialId,
                Code = ma.MaterialCode,
                Name = ma.Name,
                Brand = ma.Brand,
                Specification = ma.Specification,
                TypeId = ma.TypeId,
                Description = ma.Description,
                MinimumStock = ma.MinimumStock,
                QuantityInStock = ma.QuantityInStock
            }).ToList();
        }
        public async Task<List<ErrorInfo>> GetErrorsAsync(string languageCode)
        {
            if (!ErrorListRep.CheckConnection())
                throw new InvalidOperationException("ErrorList repository connection failed");
            var list = await ErrorListRep.GetMessagesWithOtherAsync(languageCode);
            return list.Select(er => new ErrorInfo
            {
                ErrorCode = er.ErrorCode,
                Message = er.Message,
                Category = er.Category
            }).ToList();
        }
        #endregion

        #region 設備卡片區業務邏輯 - 物料 首件 巡檢
        public async Task<int> AddReplacementRecordAsync(int equipmentId, int employeeId,
            List<(int materialId, int quantity)> materialDetails)
        {
            if (MaterialReplacementRep.CheckConnection())
            {
                try
                {
                    return await MaterialReplacementRep.AddReplacementRecordAsync(
                        equipmentId, employeeId, "MTRP0001", materialDetails);
                }
                catch (SqlException ex)
                {
                    throw new DatabaseConnectionException("新增物料更換紀錄時資料庫發生錯誤。", ex);
                }
            }

            var payload = new ReplacementPayload
            {
                EquipmentId = equipmentId,
                EmployeeId = employeeId,
                Materials = materialDetails.Select(m => new MaterialItem { MaterialId = m.materialId, Quantity = m.quantity }).ToList()
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddReplacement,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
            await _offlineCache.EnqueueAsync(op);
            throw new OfflineOperationQueuedException(op.Id);
        }

        public async Task<int?> GetCurrentTimeSlotIdAsync()
        {
            return await TimeSlotLookupRep.GetCurrentTimeSlotIdAsync(BusinessDay);
        }
        public async Task<int> AddFirstInspectionAsync(int equipmentId, int employeeId, bool result,
            string? product, string? errorCode = null, string? description = null)
        {
            if (InspectionRecordRep.CheckConnection())
            {
                try
                {
                    return await InspectionRecordRep.AddInspectionRecordAsync(
                        InspectionType.First, equipmentId, employeeId, result,
                        null, product, errorCode, description);
                }
                catch (SqlException ex)
                {
                    throw new DatabaseConnectionException("新增首件紀錄時資料庫發生錯誤。", ex);
                }
            }

            var payload = new FirstInspectionPayload
            {
                EquipmentId = equipmentId, EmployeeId = employeeId,
                Result = result, Product = product, ErrorCode = errorCode, Description = description
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddFirstInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
            await _offlineCache.EnqueueAsync(op);
            throw new OfflineOperationQueuedException(op.Id);
        }

        public async Task<int> AddRoutineInspectionAsync(int equipmentId, int employeeId, bool result,
            int timeSlotId, string? product, string? errorCode = null, string? description = null)
        {
            if (InspectionRecordRep.CheckConnection())
            {
                bool exists = await InspectionRecordRep.ExistsInspectionInSlotAsync(equipmentId, timeSlotId, BusinessDay);
                if (exists)
                    throw new BusinessRuleException("同一設備同一時段已有紀錄，不能重複新增。");

                try
                {
                    return await InspectionRecordRep.AddInspectionRecordAsync(
                        InspectionType.Routine, equipmentId, employeeId, result,
                        timeSlotId, product, errorCode, description);
                }
                catch (SqlException ex)
                {
                    throw new DatabaseConnectionException("新增巡檢紀錄時資料庫發生錯誤。", ex);
                }
            }

            var payload = new RoutineInspectionPayload
            {
                EquipmentId = equipmentId, EmployeeId = employeeId,
                Result = result, TimeSlotId = timeSlotId,
                Product = product, ErrorCode = errorCode, Description = description
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddRoutineInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
            await _offlineCache.EnqueueAsync(op);
            throw new OfflineOperationQueuedException(op.Id);
        }
        public async Task<List<int>> GetAllSlotsStatusAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId)
        {
            var slotsResult = await InspectionRecordRep.GetStatusForAllSlotsAsync(equipmentId, BusinessDay);

            var now = DateTime.Now;
            var result = new List<int>();
            for (int i = 0; i < timeSlotLookups.Count; i++)
            {
                var slot = timeSlotLookups[i];
                var (hasRecord, recordResult) = slotsResult[i]; // AI 提示與timeSlotLookups數量不符警告
                var (slotStart, slotEnd) = GetSlotBounds(slot);

                int status;
                if (now < slotStart) status = -1; // 灰
                else
                {
                    if (!hasRecord)
                    {
                        if (now >= slotStart && now < slotEnd) status = 1; // 黃
                        else status = 2; // 紅
                    }
                    else status = recordResult ? 0 : 2; // 綠 : 紅
                }
                result.Add(status);
            }
            return result;
        }
        public async Task CheckAndInsertMissedInspectionAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId)
        {
            var endedSlots = timeSlotLookups.Where(slot =>
            {
                var (_, slotEnd) = GetSlotBounds(slot);
                return slotEnd <= DateTime.Now;
            });
            foreach (var slot in endedSlots)
            {
                bool exists = await InspectionRecordRep.ExistsInspectionInSlotAsync(equipmentId, slot.TimeSlotId, BusinessDay);
                if (!exists)
                {
                    try
                    {
                        // 補上一筆逾時未巡檢紀錄 (以管理員為記錄)
                        await InspectionRecordRep.AddInspectionRecordAsync(
                            InspectionType.Routine, equipmentId, 1, false,
                            slot.TimeSlotId, null, "RTIN0001", null);
                    }
                    catch (SqlException ex)
                    {
                        throw new DatabaseConnectionException($"補填時段 {slot.TimeSlotId} 失敗。", ex);
                    }
                }
            }
        }

        // 跨日邏輯: 跨整點 EndAt + 1；跨日 StartAt, EndAt + 1
        private (DateTime slotStart, DateTime slotEnd) GetSlotBounds(TimeSlotLookup slot)
        {
            var slotStart = BusinessDay.Date.Add(slot.StartAt);
            var slotEnd   = BusinessDay.Date.Add(slot.EndAt);
            if (slot.IsCrossDay)
            {
                slotEnd = slotEnd.AddDays(1);
                if (slot.EndAt > slot.StartAt)
                    slotStart = slotStart.AddDays(1);
            }
            return (slotStart, slotEnd);
        }

        #endregion









        // 測試用
        public async Task Demo()
        {
            // 查詢
            //var devs = await ErrorListRep.GetMessagesWithOtherAsync("zh-TW"); //zh-TW INSP0001
            //foreach (var dev in devs) { Debug.WriteLine($"{dev.LanguageCode} - {dev.Message}"); }
            //Debug.WriteLine($"{devs}");
            //foreach (var (ErrorCode, Message, Category) in devs) { Debug.WriteLine($"{ErrorCode} - {Message}"); }

            var timeslots = (await TimeSlotLookupRep.GetAllAsync()).ToList();
            var timeslotstatus = await GetAllSlotsStatusAsync(timeslots, 1);
            foreach (var slot in timeslotstatus)
            {
                Debug.WriteLine($"timeslotstatus = {slot}");
            }

            //await CheckAndInsertMissedInspectionAsync(timeslots, 1, 1);
            // 插入

            //var firstInspId = await CreateFirstInspectionAsync(1, 1, true,"ABC-123", null);
            //var routineInspId = await CreateRoutineInspectionAsync(1, 1, false, 3, "CDE-456", "INSP0002");

            //Debug.WriteLine($"新增成功，firstInspId = {firstInspId}");
            //Debug.WriteLine($"新增成功，routineInspId = {routineInspId}");
            //var replacementId = await MaterialReplacementRep.AddReplacementRecordAsync(
            //                    equipmentId: 3,
            //                    employeeId: 2,
            //                    errorCode: "MTRP0001",
            //                    details: new List<(int materialId, int quantity)>
            //                    {
            //                        (materialId: 1, quantity: 1),
            //                        (materialId: 3, quantity: 1),
            //                        (materialId: 4, quantity: 3),
            //                    }
            //                );
            //Debug.WriteLine($"新增成功，ReplacementId = {replacementId}");
            // 更新


            // 刪除


        }
    }
}
