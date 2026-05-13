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
using System.Data;
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
        private readonly IEquipmentRepository EquipmentRep;
        private readonly IEmployeeRepository EmployeeRep;
        private readonly IMaterialRepository MaterialRep;
        private readonly IErrorListRepository ErrorListRep;
        private readonly IMaterialReplacementRepository MaterialReplacementRep;
        private readonly ITimeSlotLookupRepository TimeSlotLookupRep;
        private readonly IInspectionRecordRepository InspectionRecordRep;
        private readonly ITuningRecordRepository TuningRecordRep;
        private readonly IRolePermissionRepository RolePermissionRep;

        private readonly IOfflineCacheService _offlineCache;

        public DataService(IEquipmentRepository equipmentrep, IEmployeeRepository workerrep, IMaterialRepository materialrep,
            IErrorListRepository errorListRep, IMaterialReplacementRepository materialReplacementRep,
            IInspectionRecordRepository inspectionRecordRep, ITimeSlotLookupRepository timeSlotLookupRep,
            IOfflineCacheService offlineCache, IRolePermissionRepository rolePermissionRep, ITuningRecordRepository tuningRecordRep)
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
            TuningRecordRep = tuningRecordRep;
        }

#if DEBUG
        public async Task Demo()
        {
            // 查詢
            //var devs = await ErrorListRep.GetMessagesWithOtherAsync("zh-TW"); //zh-TW INSP0001
            //foreach (var dev in devs) { Debug.WriteLine($"{dev.LanguageCode} - {dev.Message}"); }
            //Debug.WriteLine($"{devs}");
            //foreach (var (ErrorCode, Message, Category) in devs) { Debug.WriteLine($"{ErrorCode} - {Message}"); }

            var roles = (await RolePermissionRep.GetAllRolesAsync().ConfigureAwait(false)).ToList();
            Debug.WriteLine($"roles.Count = {roles.Count}");
            //foreach (var nrole in roles)
            //{
            //    Debug.WriteLine($"nrole.Name = {nrole.Name}");
            //    foreach (var npermission in nrole.RolePermissions)
            //    { Debug.WriteLine($"permission = {npermission.PermissionId}"); }
            //}

        }
#endif

        #region 清單查詢與 Mapping
        public async Task<List<DeviceInfo>> GetDevicesAsync()
        {
            if (!await EquipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetDevicesAsync] 設備清單 Repository 連線失敗");
            var list = await EquipmentRep.GetAllAsync().ConfigureAwait(false);
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
            if (!await EmployeeRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetUsersAsync] 人員清單 Repository 連線失敗");
            var list = await EmployeeRep.GetAllAsync().ConfigureAwait(false);
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
            if (!await MaterialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetMaterialsAsync] 材料清單 Repository 連線失敗");
            var list = await MaterialRep.GetAllAsync().ConfigureAwait(false);
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
            if (!await ErrorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetErrorsAsync] 錯誤清單 Repository 連線失敗");
            var list = await ErrorListRep.GetMessagesWithOtherAsync(languageCode).ConfigureAwait(false);
            return list.Select(er => new ErrorInfo
            {
                ErrorCode = er.ErrorCode,
                Message = er.Message,
                TypeId = er.TypeId
            }).ToList();
        }
        public async Task<List<TimeSlotLookup>> GetTimeSlotsAsync()
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetTimeSlotsAsync] 時段清單 Repository 連線失敗");
            var list = (await TimeSlotLookupRep.GetAllAsync().ConfigureAwait(false)).OrderBy(s => s.TimeSlotId);
            return list.ToList();
        }
        #endregion

        #region 設備卡片區業務邏輯 - 物料 首件 巡檢
        public async Task<int> AddReplacementRecordAsync(int equipmentId, int employeeId,
            List<(int materialId, int quantity)> materialDetails)
        {
            if (await MaterialReplacementRep.CheckConnectionAsync().ConfigureAwait(false))
            {
                try
                {
                    return await MaterialReplacementRep.AddReplacementRecordAsync(
                        equipmentId, employeeId, "MTRP0001", materialDetails);
                }
                catch (SqlException ex)
                {
                    throw new DatabaseConnectionException("[AddReplacementRecordAsync] 新增物料更換紀錄失敗：資料庫錯誤", ex);
                }
                catch (TimeoutException tex) { throw new DatabaseConnectionException("[AddReplacementRecordAsync] 新增物料更換紀錄失敗：連線逾時", tex); }
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
            _ = Task.Run(async () =>
            {
                try { await _offlineCache.EnqueueAsync(op).ConfigureAwait(false); }
                catch (Exception ex) { Debug.WriteLine($"[OfflineCache] EnqueueAsync failed: {ex.Message}"); }
            });

            throw new OfflineOperationQueuedException(op.Id);
        }

        public async Task<int?> GetCurrentTimeSlotIdAsync()
        {
            return await TimeSlotLookupRep.GetCurrentTimeSlotIdAsync(BusinessDay).ConfigureAwait(false);
        }
        public int? GetCurrentTimeSlotId(List<TimeSlotLookup> timeslots)
        {
            return TimeSlotLookupRep.GetCurrentTimeSlotId(BusinessDay, timeslots);
        }
        public async Task<int> AddFirstInspectionAsync(int equipmentId, int employeeId, bool result,
            string? product, string? errorCode = null, string? description = null)
        {
            if (await InspectionRecordRep.CheckConnectionAsync().ConfigureAwait(false))
            {
                try
                {
                    return await InspectionRecordRep.AddInspectionRecordAsync(
                        InspectionType.First, equipmentId, employeeId, result,
                        null, product, errorCode, description);
                }
                catch (SqlException ex)
                {
                    throw new DatabaseConnectionException("[AddFirstInspectionAsync] 新增首件紀錄失敗：資料庫錯誤", ex);
                }
                catch (TimeoutException tex) { throw new DatabaseConnectionException("[AddFirstInspectionAsync] 新增首件紀錄失敗：連線逾時", tex); }
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
            _ = Task.Run(async () =>
            {
                try { await _offlineCache.EnqueueAsync(op).ConfigureAwait(false); }
                catch (Exception ex) { Debug.WriteLine($"[OfflineCache] EnqueueAsync failed: {ex.Message}"); }
            });

            throw new OfflineOperationQueuedException(op.Id);
        }

        public async Task<int> AddRoutineInspectionAsync(int equipmentId, int employeeId, bool result,
            int timeSlotId, string? product, string? errorCode = null, string? description = null)
        {
            if (await InspectionRecordRep.CheckConnectionAsync().ConfigureAwait(false))
            {
                bool exists = await InspectionRecordRep.ExistsInspectionInSlotAsync(equipmentId, timeSlotId, BusinessDay).ConfigureAwait(false);
                if (exists)
                    throw new BusinessRuleException("[AddRoutineInspectionAsync] 同一設備同一時段已有紀錄，不能重複新增");

                try
                {
                    return await InspectionRecordRep.AddInspectionRecordAsync(
                        InspectionType.Routine, equipmentId, employeeId, result,
                        timeSlotId, product, errorCode, description);
                }
                catch (SqlException ex)
                {
                    throw new DatabaseConnectionException("[AddRoutineInspectionAsync] 新增巡檢紀錄失敗：資料庫錯誤", ex);
                }
                catch (TimeoutException tex) { throw new DatabaseConnectionException("[AddRoutineInspectionAsync] 新增巡檢紀錄失敗：連線逾時", tex); }
            }

            /// 進入離線DB前，確認local DB是否有相同紀錄 *** 只在同一個工作日有效 *** 同步確認 RoutineInspectionSyncHandler
            //var pending = await _offlineCache.GetPendingAsync().ConfigureAwait(false);
            //bool existsInQueue = pending
            //    .Where(p => p.OperationType == PendingOperationType.AddRoutineInspection)
            //    .Any(p =>
            //    {
            //        var pl = JsonSerializer.Deserialize<RoutineInspectionPayload>(p.PayloadJson);
            //        return pl?.EquipmentId == equipmentId && pl?.TimeSlotId == timeSlotId;
            //    });
            //if (existsInQueue)
            //    throw new BusinessRuleException("同一設備同一時段已有紀錄，不能重複新增。");
            /// 進入離線DB前，確認local DB是否有相同紀錄 *** 只在同一個工作日有效 ***
            
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
            _ = Task.Run(async () =>
            {
                try { await _offlineCache.EnqueueAsync(op).ConfigureAwait(false); }
                catch (Exception ex) { Debug.WriteLine($"[OfflineCache] EnqueueAsync failed: {ex.Message}"); }
            });

            throw new OfflineOperationQueuedException(op.Id);
        }
        public async Task<List<int>> GetAllSlotsStatusAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId)
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllSlotsStatusAsync] 時段 Repository 連線失敗");

            var slotsResult = await InspectionRecordRep.GetStatusForAllSlotsAsync(equipmentId, BusinessDay).ConfigureAwait(false);

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
            if (!(await InspectionRecordRep.CheckConnectionAsync().ConfigureAwait(false)))
                throw new InvalidOperationException("[CheckAndInsertMissedInspectionAsync] 巡檢紀錄 Repository 連線失敗");

            // 搜尋已結束的時段
            var endedSlots = timeSlotLookups.Where(slot =>
            {
                var (_, slotEnd) = GetSlotBounds(slot);
                return slotEnd <= DateTime.Now;
            });

            // 確認每個結束時段是否有紀錄，沒有紀錄則上傳逾時紀錄
            foreach (var slot in endedSlots)
            {
                try
                {
                    bool exists = await InspectionRecordRep.ExistsInspectionInSlotAsync(equipmentId, slot.TimeSlotId, BusinessDay).ConfigureAwait(false);
                    if (!exists)
                    {
                        // 補上一筆逾時未巡檢紀錄 (以管理員為記錄)
                        await InspectionRecordRep.AddInspectionRecordAsync(
                            InspectionType.Routine, equipmentId, 1, false,
                            slot.TimeSlotId, null, "RTIN0001", null);
                    }
                }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    // 競態：巡檢紀錄已由其他操作寫入，略過
                }
                catch (SqlException ex)
                {
                    throw new DatabaseConnectionException($"[CheckAndInsertMissedInspectionAsync] 補填時段 {slot.TimeSlotId} 失敗：資料庫錯誤", ex);
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

        public async Task<int> AddTeachingRecordAsync(int equipmentId, int employeeId, int durationSec, string? product)
        {
            if (await TuningRecordRep.CheckConnectionAsync().ConfigureAwait(false))
            {
                try
                {
                    return await TuningRecordRep.AddTuningRecordAsync(
                        TuningType.Teaching, equipmentId, employeeId, durationSec, product);
                }
                catch (SqlException ex)
                {
                    throw new DatabaseConnectionException("[AddTeachingRecordAsync] 新增帶點紀錄失敗：資料庫錯誤", ex);
                }
                catch (TimeoutException tex) { throw new DatabaseConnectionException("[AddTeachingRecordAsync] 新增帶點紀錄失敗：連線逾時", tex); }
            }

            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddTuning,
                PayloadJson = JsonSerializer.Serialize(new TuningPayload
                {
                    TuningType = TuningType.Teaching,
                    EquipmentId = equipmentId, EmployeeId = employeeId,
                    DurationSec = durationSec, Product = product,
                    OperatedAt = DateTime.Now
                })
            };
            _ = Task.Run(async () =>
            {
                try { await _offlineCache.EnqueueAsync(op).ConfigureAwait(false); }
                catch (Exception ex) { Debug.WriteLine($"[OfflineCache] EnqueueAsync failed: {ex.Message}"); }
            });
            throw new OfflineOperationQueuedException(op.Id);
        }
        public async Task<int> AddOffsetRecordAsync(int equipmentId, int employeeId, int durationSec, string? product)
        {
            if (await TuningRecordRep.CheckConnectionAsync().ConfigureAwait(false))
            {
                try
                {
                    return await TuningRecordRep.AddTuningRecordAsync(
                        TuningType.Offset, equipmentId, employeeId, durationSec, product);
                }
                catch (SqlException ex)
                {
                    throw new DatabaseConnectionException("[AddOffsetRecordAsync] 新增調品質紀錄失敗：資料庫錯誤", ex);
                }
                catch (TimeoutException tex) { throw new DatabaseConnectionException("[AddOffsetRecordAsync] 新增調品質紀錄失敗：連線逾時", tex); }
            }

            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddTuning,
                PayloadJson = JsonSerializer.Serialize(new TuningPayload
                {
                    TuningType = TuningType.Offset,
                    EquipmentId = equipmentId, EmployeeId = employeeId,
                    DurationSec = durationSec, Product = product,
                    OperatedAt = DateTime.Now
                })
            };
            _ = Task.Run(async () =>
            {
                try { await _offlineCache.EnqueueAsync(op).ConfigureAwait(false); }
                catch (Exception ex) { Debug.WriteLine($"[OfflineCache] EnqueueAsync failed: {ex.Message}"); }
            });
            throw new OfflineOperationQueuedException(op.Id);
        }

        #endregion

        #region 設定：設備 CRUD

        public async Task<List<Equipment>> GetAllEquipmentAsync()
        {
            if (!await EquipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllEquipmentAsync] 設備 Repository 連線失敗");
            return (await EquipmentRep.GetAllAsync().ConfigureAwait(false)).ToList();
        }

        public async Task AddEquipmentAsync(EquipmentFormDto dto)
        {
            if (!await EquipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddEquipmentAsync] 設備 Repository 連線失敗");
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
            await EquipmentRep.AddAsync(entity).ConfigureAwait(false);
        }

        public async Task UpdateEquipmentAsync(EquipmentFormDto dto)
        {
            if (!await EquipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateEquipmentAsync] 設備 Repository 連線失敗");
            var entity = await EquipmentRep.GetByIdAsync(dto.Id!.Value).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[UpdateEquipmentAsync] 找不到設備 ID={dto.Id}");
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
            await EquipmentRep.UpdateAsync(entity).ConfigureAwait(false);
        }

        public async Task DeleteEquipmentAsync(int id)
        {
            if (!await EquipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteEquipmentAsync] 設備 Repository 連線失敗");
            await EquipmentRep.DeleteAsync(id).ConfigureAwait(false);
        }

        public async Task<List<EquipmentType>> GetEquipmentTypesAsync()
        {
            if (!await EquipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetEquipmentTypesAsync] 設備 Repository 連線失敗");
            return await EquipmentRep.GetEquipmentTypesAsync().ConfigureAwait(false);
        }

        #endregion

        #region 設定：員工 CRUD

        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            if (!await EmployeeRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllEmployeesAsync] 人員 Repository 連線失敗");
            return (await EmployeeRep.GetAllAsync().ConfigureAwait(false)).ToList();
        }

        public async Task AddEmployeeAsync(EmployeeFormDto dto)
        {
            if (!await EmployeeRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddEmployeeAsync] 人員 Repository 連線失敗");
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
            await EmployeeRep.AddAsync(entity).ConfigureAwait(false);
        }

        public async Task UpdateEmployeeAsync(EmployeeFormDto dto)
        {
            if (!await EmployeeRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateEmployeeAsync] 人員 Repository 連線失敗");
            var entity = await EmployeeRep.GetByIdAsync(dto.Id!.Value).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[UpdateEmployeeAsync] 找不到人員 ID={dto.Id}");
            entity.UserId = dto.UserId;
            entity.Name = dto.Name;
            if (!string.IsNullOrEmpty(dto.Password))
                entity.Password = dto.Password;
            entity.RoleId = dto.RoleId;
            entity.CardId = dto.CardId;
            entity.Email = dto.Email;
            entity.DepartmentId = dto.DepartmentId;
            entity.UpdateAt = DateTime.Now;
            await EmployeeRep.UpdateAsync(entity).ConfigureAwait(false);
        }

        public async Task DeleteEmployeeAsync(int id)
        {
            if (!await EmployeeRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteEmployeeAsync] 人員 Repository 連線失敗");
            await EmployeeRep.DeleteAsync(id).ConfigureAwait(false);
        }

        #endregion

        #region 設定：材料 CRUD

        public async Task<List<Material>> GetAllMaterialsAsync()
        {
            if (!await MaterialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllMaterialsAsync] 材料 Repository 連線失敗");
            return (await MaterialRep.GetAllAsync().ConfigureAwait(false)).ToList();
        }

        public async Task<List<MaterialType>> GetMaterialTypesAsync()
        {
            if (!await MaterialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetMaterialTypesAsync] 材料 Repository 連線失敗");
            return await MaterialRep.GetMaterialTypesAsync().ConfigureAwait(false);
        }

        public async Task AddMaterialAsync(MaterialFormDto dto)
        {
            if (!await MaterialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddMaterialAsync] 材料 Repository 連線失敗");
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
            await MaterialRep.AddAsync(entity).ConfigureAwait(false);
        }

        public async Task UpdateMaterialAsync(MaterialFormDto dto)
        {
            if (!await MaterialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateMaterialAsync] 材料 Repository 連線失敗");
            var entity = await MaterialRep.GetByIdAsync(dto.Id!.Value).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[UpdateMaterialAsync] 找不到材料 ID={dto.Id}");
            entity.MaterialCode = dto.MaterialCode;
            entity.Name = dto.Name;
            entity.Brand = dto.Brand;
            entity.Specification = dto.Specification;
            entity.TypeId = dto.TypeId;
            entity.Description = dto.Description;
            entity.MinimumStock = dto.MinimumStock;
            entity.QuantityInStock = dto.QuantityInStock;
            entity.UpdateAt = DateTime.Now;
            await MaterialRep.UpdateAsync(entity).ConfigureAwait(false);
        }

        public async Task DeleteMaterialAsync(int id)
        {
            if (!await MaterialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteMaterialAsync] 材料 Repository 連線失敗");
            await MaterialRep.DeleteAsync(id).ConfigureAwait(false);
        }

        #endregion

        #region 設定：錯誤清單 CRUD

        public async Task<List<ErrorList>> GetAllErrorListsAsync()
        {
            if (!await ErrorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllErrorListsAsync] 錯誤清單 Repository 連線失敗");
            return await ErrorListRep.GetAllWithTranslationsAsync().ConfigureAwait(false);
        }
        public async Task<List<ListType>> GetListTypesAsync()
        {
            if (!await ErrorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetListTypesAsync] 錯誤清單 Repository 連線失敗");
            return await ErrorListRep.GetListTypesAsync().ConfigureAwait(false);
        }

        public async Task AddErrorListAsync(ErrorListFormDto dto)
        {
            if (!await ErrorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddErrorListAsync] 錯誤清單 Repository 連線失敗");
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
            await ErrorListRep.AddErrorAsync(error, translations).ConfigureAwait(false);
        }

        public async Task UpdateErrorListAsync(ErrorListFormDto dto)
        {
            if (!await ErrorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateErrorListAsync] 錯誤清單 Repository 連線失敗");
            await ErrorListRep.UpdateErrorListAsync(dto).ConfigureAwait(false);
        }

        public async Task DeleteErrorListAsync(int id)
        {
            if (!await ErrorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteErrorListAsync] 錯誤清單 Repository 連線失敗");
            var entity = await ErrorListRep.GetByIdAsync(id).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[DeleteErrorListAsync] 找不到錯誤清單 ID={id}");
            await ErrorListRep.DeleteErrorAsync(entity.ErrorCode).ConfigureAwait(false);
        }

        #endregion

        #region 設定：巡檢時段 CRUD 
        public async Task AddTimeSlotAsync(TimeSlotFormDto dto)
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddTimeSlotAsync] 時段 Repository 連線失敗");
            var entity = new TimeSlotLookup
            {
                TimeSlotId = dto.TimeSlotId,
                StartAt = dto.StartAt,
                EndAt = dto.EndAt,
                IsCrossDay = dto.IsCrossDay,
                Label = dto.Label
            };
            await TimeSlotLookupRep.AddAsync(entity).ConfigureAwait(false);
        }
        public async Task UpdateTimeSlotAsync(TimeSlotFormDto dto)
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateTimeSlotAsync] 時段 Repository 連線失敗");
            var entity = await TimeSlotLookupRep.GetByIdAsync(dto.TimeSlotId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[UpdateTimeSlotAsync] 找不到時段 ID={dto.TimeSlotId}");
            entity.StartAt = dto.StartAt;
            entity.EndAt = dto.EndAt;
            entity.IsCrossDay = dto.IsCrossDay;
            entity.Label = dto.Label;
            await TimeSlotLookupRep.UpdateAsync(entity).ConfigureAwait(false);
        }
        public async Task DeleteTimeSlotAsync(int timeSlotId)
        {
            if (!await TimeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteTimeSlotAsync] 時段 Repository 連線失敗");
            await TimeSlotLookupRep.DeleteAsync(timeSlotId).ConfigureAwait(false);
        }

        #endregion

        #region 角色與權限 
        public async Task<List<Models.Role>> GetAllRolesAsync()
        {
            if (!await RolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllRolesAsync] 角色權限 Repository 連線失敗");
            return await RolePermissionRep.GetAllRolesAsync().ConfigureAwait(false);
        }

        #endregion

        #region 設定：角色權限 CRUD
        public async Task<List<Models.Permission>> GetAllPermissionsAsync()
        {
            if (!await RolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllPermissionsAsync] 角色權限 Repository 連線失敗");
            return await RolePermissionRep.GetAllPermissionsAsync().ConfigureAwait(false);
        }

        public async Task AddRoleAsync(RoleFormDto dto)
        {
            if (!await RolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddRoleAsync] 角色權限 Repository 連線失敗");
            await RolePermissionRep.AddRoleWithPermissionsAsync(dto.RoleId, dto.Name, dto.Description, dto.SelectedPermissionIds).ConfigureAwait(false);
        }

        public async Task UpdateRoleAsync(RoleFormDto dto)
        {
            if (!await RolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateRoleAsync] 角色權限 Repository 連線失敗");
            await RolePermissionRep.UpdateRoleWithPermissionsAsync(dto.Id!.Value, dto.Name, dto.Description, dto.SelectedPermissionIds).ConfigureAwait(false);
        }

        public async Task DeleteRoleAsync(int id)
        {
            if (!await RolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteRoleAsync] 角色權限 Repository 連線失敗");
            if (await RolePermissionRep.HasEmployeesByRoleAsync(id).ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteRoleAsync] 此角色有員工使用，無法刪除");
            await RolePermissionRep.DeleteAsync(id).ConfigureAwait(false);
        }

        #endregion
    }
}
