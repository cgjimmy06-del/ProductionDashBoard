using FProductionDashBoard.Dtos;
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
        public IRolePermissionRepository RolePermissionRep { get; }

        private readonly IOfflineCacheService _offlineCache;

        public DataService(IEquipmentRepository equipmentrep, IEmployeeRepository workerrep, IMaterialRepository materialrep,
            IErrorListRepository errorListRep, IMaterialReplacementRepository materialReplacementRep,
            IInspectionRecordRepository inspectionRecordRep, ITimeSlotLookupRepository timeSlotLookupRep,
            IOfflineCacheService offlineCache, IRolePermissionRepository rolePermissionRep)
        {
            EquipmentRep = equipmentrep;
            EmployeeRep = workerrep;
            MaterialRep = materialrep;
            ErrorListRep = errorListRep;
            MaterialReplacementRep = materialReplacementRep;
            InspectionRecordRep = inspectionRecordRep;
            TimeSlotLookupRep = timeSlotLookupRep;
            _offlineCache = offlineCache;
            RolePermissionRep = rolePermissionRep;
        }

        #region 測試用
        public async Task Demo()
        {
            // 查詢
            //var devs = await ErrorListRep.GetMessagesWithOtherAsync("zh-TW"); //zh-TW INSP0001
            //foreach (var dev in devs) { Debug.WriteLine($"{dev.LanguageCode} - {dev.Message}"); }
            //Debug.WriteLine($"{devs}");
            //foreach (var (ErrorCode, Message, Category) in devs) { Debug.WriteLine($"{ErrorCode} - {Message}"); }

            var roles = (await RolePermissionRep.GetAllRolesAsync()).ToList();
            foreach (var nrole in roles)
            {
                Debug.WriteLine($"nrole.Name = {nrole.Name}");
                foreach (var npermission in nrole.RolePermissions)
                { Debug.WriteLine($"permission = {npermission.PermissionId}"); }
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
        #endregion

        #region 清單查詢與 Mapping
        public async Task<List<DeviceInfo>> GetDevicesAsync()
        {
            if (!await EquipmentRep.CheckConnectionAsync())
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
            if (!await EmployeeRep.CheckConnectionAsync())
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
            if (!await MaterialRep.CheckConnectionAsync())
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
            if (!await ErrorListRep.CheckConnectionAsync())
                throw new InvalidOperationException("ErrorList repository connection failed");
            var list = await ErrorListRep.GetMessagesWithOtherAsync(languageCode);
            return list.Select(er => new ErrorInfo
            {
                ErrorCode = er.ErrorCode,
                Message = er.Message,
                TypeId = er.TypeId
            }).ToList();
        }
        public async Task<List<TimeSlotLookup>> GetTimeSlotsAsync()
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync())
                throw new InvalidOperationException("TimeSlotLookup repository connection failed");
            var list = (await TimeSlotLookupRep.GetAllAsync()).OrderBy(s => s.TimeSlotId);
            return list.ToList();
        }
        #endregion

        #region 設備卡片區業務邏輯 - 物料 首件 巡檢
        public async Task<int> AddReplacementRecordAsync(int equipmentId, int employeeId,
            List<(int materialId, int quantity)> materialDetails)
        {
            if (await MaterialReplacementRep.CheckConnectionAsync())
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
                Materials = materialDetails.Select(m => new MaterialItem { MaterialId = m.materialId, Quantity = m.quantity }).ToList(),
                OperatedAt = DateTime.Now
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddReplacement,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
            _ = Task.Run(async () => await _offlineCache.EnqueueAsync(op));

            throw new OfflineOperationQueuedException(op.Id);
        }

        public async Task<int?> GetCurrentTimeSlotIdAsync()
        {
            return await TimeSlotLookupRep.GetCurrentTimeSlotIdAsync(BusinessDay);
        }
        public int? GetCurrentTimeSlotId(List<TimeSlotLookup> timeslots)
        {
            return TimeSlotLookupRep.GetCurrentTimeSlotId(BusinessDay, timeslots);
        }
        public async Task<int> AddFirstInspectionAsync(int equipmentId, int employeeId, bool result,
            string? product, string? errorCode = null, string? description = null)
        {
            if (await InspectionRecordRep.CheckConnectionAsync())
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
                EquipmentId = equipmentId,
                EmployeeId = employeeId,
                Result = result,
                Product = product,
                ErrorCode = errorCode,
                Description = description,
                OperatedAt = DateTime.Now
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddFirstInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
            _ = Task.Run(async () => await _offlineCache.EnqueueAsync(op));

            throw new OfflineOperationQueuedException(op.Id);
        }

        public async Task<int> AddRoutineInspectionAsync(int equipmentId, int employeeId, bool result,
            int timeSlotId, string? product, string? errorCode = null, string? description = null)
        {
            if (await InspectionRecordRep.CheckConnectionAsync())
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
                EquipmentId = equipmentId,
                EmployeeId = employeeId,
                Result = result,
                TimeSlotId = timeSlotId,
                Product = product,
                ErrorCode = errorCode,
                Description = description,
                OperatedAt = DateTime.Now
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddRoutineInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
            _ = Task.Run(async () => await _offlineCache.EnqueueAsync(op));

            throw new OfflineOperationQueuedException(op.Id);
        }
        public async Task<List<int>> GetAllSlotsStatusAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId)
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync())
                throw new InvalidOperationException("TimeSlotLookup repository connection failed");

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
            if (!(await InspectionRecordRep.CheckConnectionAsync()))
                throw new InvalidOperationException("InspectionRecordRep repository connection failed");

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
            var slotEnd = BusinessDay.Date.Add(slot.EndAt);
            if (slot.IsCrossDay)
            {
                slotEnd = slotEnd.AddDays(1);
                if (slot.EndAt > slot.StartAt)
                    slotStart = slotStart.AddDays(1);
            }
            return (slotStart, slotEnd);
        }

        #endregion

        #region 設定：設備 CRUD

        public async Task<List<Equipment>> GetAllEquipmentAsync()
        {
            if (!await EquipmentRep.CheckConnectionAsync())
                throw new InvalidOperationException("Equipment repository connection failed");
            return (await EquipmentRep.GetAllAsync()).ToList();
        }

        public async Task AddEquipmentAsync(EquipmentFormDto dto)
        {
            if (!await EquipmentRep.CheckConnectionAsync())
                throw new InvalidOperationException("Equipment repository connection failed");
            var entity = new Equipment
            {
                Code = dto.Code,
                Name = dto.Name,
                Ip = dto.Ip,
                Port = dto.Port,
                TypeId = dto.TypeId,
                Factory = dto.Factory,
                Building = dto.Building,
                Floor = dto.Floor,
                DepartmentId = dto.DepartmentId,
                Description = dto.Description
            };
            await EquipmentRep.AddAsync(entity);
        }

        public async Task UpdateEquipmentAsync(EquipmentFormDto dto)
        {
            if (!await EquipmentRep.CheckConnectionAsync())
                throw new InvalidOperationException("Equipment repository connection failed");
            var entity = await EquipmentRep.GetByIdAsync(dto.Id!.Value)
                ?? throw new InvalidOperationException($"Equipment id={dto.Id} not found");
            entity.Code = dto.Code;
            entity.Name = dto.Name;
            entity.Ip = dto.Ip;
            entity.Port = dto.Port;
            entity.TypeId = dto.TypeId;
            entity.Factory = dto.Factory;
            entity.Building = dto.Building;
            entity.Floor = dto.Floor;
            entity.DepartmentId = dto.DepartmentId;
            entity.Description = dto.Description;
            entity.UpdateAt = DateTime.Now;
            await EquipmentRep.UpdateAsync(entity);
        }

        public async Task DeleteEquipmentAsync(int id)
        {
            if (!await EquipmentRep.CheckConnectionAsync())
                throw new InvalidOperationException("Equipment repository connection failed");
            await EquipmentRep.DeleteAsync(id);
        }

        public async Task<List<EquipmentType>> GetEquipmentTypesAsync()
        {
            if (!await EquipmentRep.CheckConnectionAsync())
                throw new InvalidOperationException("Equipment repository connection failed");
            return await EquipmentRep.GetEquipmentTypesAsync();
        }

        #endregion

        #region 設定：員工 CRUD

        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            if (!await EmployeeRep.CheckConnectionAsync())
                throw new InvalidOperationException("Employee repository connection failed");
            return (await EmployeeRep.GetAllAsync()).ToList();
        }

        public async Task AddEmployeeAsync(EmployeeFormDto dto)
        {
            if (!await EmployeeRep.CheckConnectionAsync())
                throw new InvalidOperationException("Employee repository connection failed");
            var entity = new Employee
            {
                UserId = dto.UserId,
                Name = dto.Name,
                Password = dto.Password,
                RoleId = dto.RoleId,
                CardId = dto.CardId,
                Email = dto.Email,
                DepartmentId = dto.DepartmentId
            };
            await EmployeeRep.AddAsync(entity);
        }

        public async Task UpdateEmployeeAsync(EmployeeFormDto dto)
        {
            if (!await EmployeeRep.CheckConnectionAsync())
                throw new InvalidOperationException("Employee repository connection failed");
            var entity = await EmployeeRep.GetByIdAsync(dto.Id!.Value)
                ?? throw new InvalidOperationException($"Employee id={dto.Id} not found");
            entity.UserId = dto.UserId;
            entity.Name = dto.Name;
            if (!string.IsNullOrEmpty(dto.Password))
                entity.Password = dto.Password;
            entity.RoleId = dto.RoleId;
            entity.CardId = dto.CardId;
            entity.Email = dto.Email;
            entity.DepartmentId = dto.DepartmentId;
            entity.UpdateAt = DateTime.Now;
            await EmployeeRep.UpdateAsync(entity);
        }

        public async Task DeleteEmployeeAsync(int id)
        {
            if (!await EmployeeRep.CheckConnectionAsync())
                throw new InvalidOperationException("Employee repository connection failed");
            await EmployeeRep.DeleteAsync(id);
        }

        #endregion

        #region 設定：材料 CRUD

        public async Task<List<Material>> GetAllMaterialsAsync()
        {
            if (!await MaterialRep.CheckConnectionAsync())
                throw new InvalidOperationException("Material repository connection failed");
            return (await MaterialRep.GetAllAsync()).ToList();
        }

        public async Task<List<MaterialType>> GetMaterialTypesAsync()
        {
            if (!await MaterialRep.CheckConnectionAsync())
                throw new InvalidOperationException("Material repository connection failed");
            return await MaterialRep.GetMaterialTypesAsync();
        }

        public async Task AddMaterialAsync(MaterialFormDto dto)
        {
            if (!await MaterialRep.CheckConnectionAsync())
                throw new InvalidOperationException("Material repository connection failed");
            var entity = new Material
            {
                MaterialCode = dto.MaterialCode,
                Name = dto.Name,
                Brand = dto.Brand,
                Specification = dto.Specification,
                TypeId = dto.TypeId,
                Description = dto.Description,
                MinimumStock = dto.MinimumStock,
                QuantityInStock = dto.QuantityInStock
            };
            await MaterialRep.AddAsync(entity);
        }

        public async Task UpdateMaterialAsync(MaterialFormDto dto)
        {
            if (!await MaterialRep.CheckConnectionAsync())
                throw new InvalidOperationException("Material repository connection failed");
            var entity = await MaterialRep.GetByIdAsync(dto.Id!.Value)
                ?? throw new InvalidOperationException($"Material id={dto.Id} not found");
            entity.MaterialCode = dto.MaterialCode;
            entity.Name = dto.Name;
            entity.Brand = dto.Brand;
            entity.Specification = dto.Specification;
            entity.TypeId = dto.TypeId;
            entity.Description = dto.Description;
            entity.MinimumStock = dto.MinimumStock;
            entity.QuantityInStock = dto.QuantityInStock;
            entity.UpdateAt = DateTime.Now;
            await MaterialRep.UpdateAsync(entity);
        }

        public async Task DeleteMaterialAsync(int id)
        {
            if (!await MaterialRep.CheckConnectionAsync())
                throw new InvalidOperationException("Material repository connection failed");
            await MaterialRep.DeleteAsync(id);
        }

        #endregion

        #region 設定：錯誤清單 CRUD

        public async Task<List<ErrorList>> GetAllErrorListsAsync()
        {
            if (!await ErrorListRep.CheckConnectionAsync())
                throw new InvalidOperationException("ErrorList repository connection failed");
            return await ErrorListRep.GetAllWithTranslationsAsync();
        }

        public async Task AddErrorListAsync(ErrorListFormDto dto)
        {
            if (!await ErrorListRep.CheckConnectionAsync())
                throw new InvalidOperationException("ErrorList repository connection failed");
            var error = new ErrorList
            {
                ErrorCode = dto.ErrorCode,
                TypeId = dto.TypeId,
                Severity = dto.Severity
            };
            var translations = new List<ErrorTranslation>();
            if (!string.IsNullOrWhiteSpace(dto.MessageZhTw))
                translations.Add(new ErrorTranslation { ErrorCode = dto.ErrorCode, LanguageCode = "zh-TW", Message = dto.MessageZhTw });
            if (!string.IsNullOrWhiteSpace(dto.MessageEnUs))
                translations.Add(new ErrorTranslation { ErrorCode = dto.ErrorCode, LanguageCode = "en-US", Message = dto.MessageEnUs });
            if (!string.IsNullOrWhiteSpace(dto.MessageViVn))
                translations.Add(new ErrorTranslation { ErrorCode = dto.ErrorCode, LanguageCode = "vi-VN", Message = dto.MessageViVn });
            await ErrorListRep.AddErrorAsync(error, translations);
        }

        public async Task UpdateErrorListAsync(ErrorListFormDto dto)
        {
            if (!await ErrorListRep.CheckConnectionAsync())
                throw new InvalidOperationException("ErrorList repository connection failed");
            await ErrorListRep.UpdateErrorListAsync(dto);
        }

        public async Task DeleteErrorListAsync(int id)
        {
            if (!await ErrorListRep.CheckConnectionAsync())
                throw new InvalidOperationException("ErrorList repository connection failed");
            var entity = await ErrorListRep.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"ErrorList id={id} not found");
            await ErrorListRep.DeleteErrorAsync(entity.ErrorCode);
        }

        #endregion

        #region 設定：巡檢時段 CRUD 
        public async Task AddTimeSlotAsync(TimeSlotFormDto dto)
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync())
                throw new InvalidOperationException("TimeSlotLookup repository connection failed");
            var entity = new TimeSlotLookup
            {
                TimeSlotId = dto.TimeSlotId,
                StartAt = dto.StartAt,
                EndAt = dto.EndAt,
                IsCrossDay = dto.IsCrossDay,
                Label = dto.Label
            };
            await TimeSlotLookupRep.AddAsync(entity);
        }
        public async Task UpdateTimeSlotAsync(TimeSlotFormDto dto)
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync())
                throw new InvalidOperationException("TimeSlotLookup repository connection failed");
            var entity = await TimeSlotLookupRep.GetByIdAsync(dto.TimeSlotId)
                ?? throw new InvalidOperationException($"TimeSlot {dto.TimeSlotId} not found");
            entity.StartAt = dto.StartAt;
            entity.EndAt = dto.EndAt;
            entity.IsCrossDay = dto.IsCrossDay;
            entity.Label = dto.Label;
            await TimeSlotLookupRep.UpdateAsync(entity);
        }
        public async Task DeleteTimeSlotAsync(int timeSlotId)
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync())
                throw new InvalidOperationException("TimeSlotLookup repository connection failed");
            await TimeSlotLookupRep.DeleteAsync(timeSlotId);
        }

        #endregion

        #region 角色與權限 
        public async Task<List<Models.Role>> GetAllRolesAsync()
        {
            if (!await RolePermissionRep.CheckConnectionAsync())
                throw new InvalidOperationException("RolePermission repository connection failed");
            return await RolePermissionRep.GetAllRolesAsync();
        }

        #endregion

        #region 設定：角色權限 CRUD
        public async Task<List<Models.Permission>> GetAllPermissionsAsync()
        {
            if (!await RolePermissionRep.CheckConnectionAsync())
                throw new InvalidOperationException("RolePermission repository connection failed");
            return await RolePermissionRep.GetAllPermissionsAsync();
        }

        public async Task AddRoleAsync(RoleFormDto dto)
        {
            if (!await RolePermissionRep.CheckConnectionAsync())
                throw new InvalidOperationException("RolePermission repository connection failed");
            await RolePermissionRep.AddRoleWithPermissionsAsync(dto.RoleId, dto.Name, dto.Description, dto.SelectedPermissionIds);
        }

        public async Task UpdateRoleAsync(RoleFormDto dto)
        {
            if (!await RolePermissionRep.CheckConnectionAsync())
                throw new InvalidOperationException("RolePermission repository connection failed");
            await RolePermissionRep.UpdateRoleWithPermissionsAsync(dto.Id!.Value, dto.Name, dto.Description, dto.SelectedPermissionIds);
        }

        public async Task DeleteRoleAsync(int id)
        {
            if (!await RolePermissionRep.CheckConnectionAsync())
                throw new InvalidOperationException("RolePermission repository connection failed");
            if (await RolePermissionRep.HasEmployeesByRoleAsync(id))
                throw new InvalidOperationException("此角色有員工使用，無法刪除");
            await RolePermissionRep.DeleteAsync(id);
        }

        #endregion
    }
}