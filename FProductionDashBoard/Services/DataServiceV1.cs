using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Models.Extra;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Repositories.ExtraDb;
using FProductionDashBoard.Services.Exceptions;
using FProductionDashBoard.Services.Offline;
using FProductionDashBoard.Services.Offline.Payloads;
using FProductionDashBoard.UiModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services.V1
{
    public class DataService : IDataService
    {
        public DateTime BusinessDay { get; set; }
        private readonly IEquipmentRepository _equipmentRep;
        private readonly IEmployeeRepository _employeeRep;
        private readonly IMaterialRepository _materialRep;
        private readonly IErrorListRepository _errorListRep;
        private readonly IMaterialReplacementRepository _materialReplacementRep;
        private readonly ITimeSlotLookupRepository _timeSlotLookupRep;
        private readonly IInspectionRecordRepository _inspectionRecordRep;
        private readonly IRolePermissionRepository _rolePermissionRep;
        private readonly IProductPartRepository _productPartRep;
        private readonly IProductRepository _productRep;
        private readonly ISopChecklistRepository _sopChecklistRep;
        private readonly IEquipmentProductRepository _equipmentProductRep;
        private readonly IOrderProductionRepository _orderProductionRep;
        private readonly IProgramTuningRecordRepository _programTuningRep;
        private readonly IScheduleRepository _scheduleRep;
        private readonly IDbContextFactory<MesDbContext> _mesFactory;
        private readonly IInfoDbRepository _infoRep;
        private readonly IDataDbRepository _dataRep;

        private readonly IOfflineCacheService _offlineCache;

        public DataService(IEquipmentRepository equipmentrep, IEmployeeRepository workerrep, IMaterialRepository materialrep,
            IErrorListRepository errorListRep, IMaterialReplacementRepository materialReplacementRep,
            IInspectionRecordRepository inspectionRecordRep, ITimeSlotLookupRepository timeSlotLookupRep,
            IOfflineCacheService offlineCache, IRolePermissionRepository rolePermissionRep,
            IProductPartRepository productPartRep, IProductRepository productRep, ISopChecklistRepository sopChecklistRep,
            IEquipmentProductRepository equipmentProductRep, IOrderProductionRepository orderProductionRep,
            IProgramTuningRecordRepository programTuningRep, IScheduleRepository scheduleRep,
            IDbContextFactory<MesDbContext> mesFactory, IInfoDbRepository infoRep, IDataDbRepository dataRep)
        {
            _equipmentRep = equipmentrep;
            _employeeRep = workerrep;
            _materialRep = materialrep;
            _errorListRep = errorListRep;
            _materialReplacementRep = materialReplacementRep;
            _inspectionRecordRep = inspectionRecordRep;
            _timeSlotLookupRep = timeSlotLookupRep;
            _offlineCache = offlineCache;
            _rolePermissionRep = rolePermissionRep;
            _productPartRep = productPartRep;
            _productRep = productRep;
            _sopChecklistRep = sopChecklistRep;
            _equipmentProductRep = equipmentProductRep;
            _orderProductionRep = orderProductionRep;
            _programTuningRep = programTuningRep;
            _scheduleRep = scheduleRep;
            _mesFactory = mesFactory;
            _infoRep = infoRep;
            _dataRep = dataRep;
        }

        #region 清單查詢與 Mapping
        public async Task<List<DeviceInfo>> GetDevicesAsync()
        {
            if (!await _equipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetDevicesAsync] 設備清單 Repository 連線失敗");
            var list = await _equipmentRep.GetAllAsync().ConfigureAwait(false);
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
            if (!await _employeeRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetUsersAsync] 人員清單 Repository 連線失敗");
            var list = await _employeeRep.GetAllAsync().ConfigureAwait(false);
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
            if (!await _materialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetMaterialsAsync] 材料清單 Repository 連線失敗");
            var list = await _materialRep.GetAllAsync().ConfigureAwait(false);
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
            if (!await _errorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetErrorsAsync] 錯誤清單 Repository 連線失敗");
            var list = await _errorListRep.GetMessagesWithOtherAsync(languageCode).ConfigureAwait(false);
            return list.Select(er => new ErrorInfo
            {
                ErrorCode = er.ErrorCode,
                Message = er.Message,
                TypeId = er.TypeId
            }).ToList();
        }
        public async Task<List<TimeSlotLookup>> GetTimeSlotsAsync()
        {
            if (!await _timeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetTimeSlotsAsync] 時段清單 Repository 連線失敗");
            var list = (await _timeSlotLookupRep.GetAllAsync().ConfigureAwait(false)).OrderBy(s => s.TimeSlotId);
            return list.ToList();
        }
        #endregion

        #region 設備卡片區業務邏輯 - 物料 首件 巡檢
        public async Task<int> AddReplacementRecordAsync(int equipmentId, int employeeId,
            List<(int materialId, int quantity)> materialDetails)
        {
            if (await _materialReplacementRep.CheckConnectionAsync().ConfigureAwait(false))
            {
                try
                {
                    return await _materialReplacementRep.AddReplacementRecordAsync(
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
            await _offlineCache.EnqueueAsync(op).ConfigureAwait(false);
            throw new OfflineOperationQueuedException(op.Id);
        }

        public async Task<int?> GetCurrentTimeSlotIdAsync()
        {
            return await _timeSlotLookupRep.GetCurrentTimeSlotIdAsync(BusinessDay).ConfigureAwait(false);
        }
        public int? GetCurrentTimeSlotId(List<TimeSlotLookup> timeslots)
        {
            return _timeSlotLookupRep.GetCurrentTimeSlotId(BusinessDay, timeslots);
        }
        public async Task<int> AddFirstInspectionAsync(int equipmentId, int employeeId, bool result,
            int? productId, string? errorCode = null, string? description = null)
        {
            if (await _inspectionRecordRep.CheckConnectionAsync().ConfigureAwait(false))
            {
                try
                {
                    return await _inspectionRecordRep.AddInspectionRecordAsync(
                        InspectionType.First, equipmentId, employeeId, result,
                        null, productId, errorCode, description);
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
                ProductId = productId,
                ErrorCode = errorCode,
                Description = description,
                OperatedAt = DateTime.Now
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddFirstInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
            await _offlineCache.EnqueueAsync(op).ConfigureAwait(false);
            throw new OfflineOperationQueuedException(op.Id);
        }

        public async Task<int> AddRoutineInspectionAsync(int equipmentId, int employeeId, bool result,
            int timeSlotId, int? productId, string? errorCode = null, string? description = null)
        {
            if (await _inspectionRecordRep.CheckConnectionAsync().ConfigureAwait(false))
            {
                bool exists = await _inspectionRecordRep.ExistsInspectionInSlotAsync(equipmentId, timeSlotId, BusinessDay).ConfigureAwait(false);
                if (exists)
                    throw new BusinessRuleException("[AddRoutineInspectionAsync] 同一設備同一時段已有紀錄，不能重複新增");

                try
                {
                    return await _inspectionRecordRep.AddInspectionRecordAsync(
                        InspectionType.Routine, equipmentId, employeeId, result,
                        timeSlotId, productId, errorCode, description);
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
                ProductId = productId,
                ErrorCode = errorCode,
                Description = description,
                OperatedAt = DateTime.Now
            };
            var op = new PendingOperation
            {
                OperationType = PendingOperationType.AddRoutineInspection,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
            await _offlineCache.EnqueueAsync(op).ConfigureAwait(false);
            throw new OfflineOperationQueuedException(op.Id);
        }
        public async Task<List<int>> GetAllSlotsStatusAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId)
        {
            if (!await _timeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllSlotsStatusAsync] 時段 Repository 連線失敗");

            var slotsResult = await _inspectionRecordRep.GetStatusForAllSlotsAsync(equipmentId, BusinessDay).ConfigureAwait(false);

            var now = DateTime.Now;
            var result = new List<int>();
            for (int i = 0; i < timeSlotLookups.Count; i++)
            {
                var slot = timeSlotLookups[i];
                var (hasRecord, recordResult) = slotsResult[i]; // AI 提示與timeSlotLookups數量不符警告
                var (slotStart, slotEnd) = slot.GetBounds(BusinessDay);

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
            if (!(await _inspectionRecordRep.CheckConnectionAsync().ConfigureAwait(false)))
                throw new InvalidOperationException("[CheckAndInsertMissedInspectionAsync] 巡檢紀錄 Repository 連線失敗");

            // 搜尋已結束的時段
            var endedSlots = timeSlotLookups.Where(slot =>
            {
                var (_, slotEnd) = slot.GetBounds(BusinessDay);
                return slotEnd <= DateTime.Now;
            });

            // 確認每個結束時段是否有紀錄，沒有紀錄則上傳逾時紀錄
            foreach (var slot in endedSlots)
            {
                try
                {
                    bool exists = await _inspectionRecordRep.ExistsInspectionInSlotAsync(equipmentId, slot.TimeSlotId, BusinessDay).ConfigureAwait(false);
                    if (!exists)
                    {
                        // 補上一筆逾時未巡檢紀錄 (以管理員為記錄)
                        await _inspectionRecordRep.AddInspectionRecordAsync(
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
        #endregion

        #region 設定：設備 CRUD

        public async Task<List<Equipment>> GetAllEquipmentAsync()
        {
            if (!await _equipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllEquipmentAsync] 設備 Repository 連線失敗");
            return await _equipmentRep.GetAllWithTypeAsync().ConfigureAwait(false);
        }

        public async Task AddEquipmentAsync(EquipmentFormDto dto)
        {
            if (!await _equipmentRep.CheckConnectionAsync().ConfigureAwait(false))
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
            await _equipmentRep.AddAsync(entity).ConfigureAwait(false);
        }

        public async Task UpdateEquipmentAsync(EquipmentFormDto dto)
        {
            if (!await _equipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateEquipmentAsync] 設備 Repository 連線失敗");
            var entity = await _equipmentRep.GetByIdAsync(dto.Id!.Value).ConfigureAwait(false)
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
            await _equipmentRep.UpdateAsync(entity).ConfigureAwait(false);
        }

        public async Task DeleteEquipmentAsync(int id)
        {
            if (!await _equipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteEquipmentAsync] 設備 Repository 連線失敗");
            await _equipmentRep.DeleteAsync(id).ConfigureAwait(false);
        }

        public async Task<List<EquipmentType>> GetEquipmentTypesAsync()
        {
            if (!await _equipmentRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetEquipmentTypesAsync] 設備 Repository 連線失敗");
            return await _equipmentRep.GetEquipmentTypesAsync().ConfigureAwait(false);
        }

        #endregion

        #region 設定：員工 CRUD

        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            if (!await _employeeRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllEmployeesAsync] 人員 Repository 連線失敗");
            return (await _employeeRep.GetAllAsync().ConfigureAwait(false)).ToList();
        }

        public async Task AddEmployeeAsync(EmployeeFormDto dto)
        {
            if (!await _employeeRep.CheckConnectionAsync().ConfigureAwait(false))
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
            await _employeeRep.AddAsync(entity).ConfigureAwait(false);
        }

        public async Task UpdateEmployeeAsync(EmployeeFormDto dto)
        {
            if (!await _employeeRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateEmployeeAsync] 人員 Repository 連線失敗");
            var entity = await _employeeRep.GetByIdAsync(dto.Id!.Value).ConfigureAwait(false)
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
            await _employeeRep.UpdateAsync(entity).ConfigureAwait(false);
        }

        public async Task DeleteEmployeeAsync(int id)
        {
            if (!await _employeeRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteEmployeeAsync] 人員 Repository 連線失敗");
            await _employeeRep.DeleteAsync(id).ConfigureAwait(false);
        }

        #endregion

        #region 設定：材料 CRUD

        public async Task<List<Material>> GetAllMaterialsAsync()
        {
            if (!await _materialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllMaterialsAsync] 材料 Repository 連線失敗");
            return await _materialRep.GetAllWithTypeAsync().ConfigureAwait(false);
        }

        public async Task<List<MaterialType>> GetMaterialTypesAsync()
        {
            if (!await _materialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetMaterialTypesAsync] 材料 Repository 連線失敗");
            return await _materialRep.GetMaterialTypesAsync().ConfigureAwait(false);
        }

        public async Task AddMaterialAsync(MaterialFormDto dto)
        {
            if (!await _materialRep.CheckConnectionAsync().ConfigureAwait(false))
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
            await _materialRep.AddAsync(entity).ConfigureAwait(false);
        }

        public async Task UpdateMaterialAsync(MaterialFormDto dto)
        {
            if (!await _materialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateMaterialAsync] 材料 Repository 連線失敗");
            var entity = await _materialRep.GetByIdAsync(dto.Id!.Value).ConfigureAwait(false)
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
            await _materialRep.UpdateAsync(entity).ConfigureAwait(false);
        }

        public async Task DeleteMaterialAsync(int id)
        {
            if (!await _materialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteMaterialAsync] 材料 Repository 連線失敗");
            await _materialRep.DeleteAsync(id).ConfigureAwait(false);
        }

        #endregion

        #region 設定：錯誤清單 CRUD

        public async Task<List<ErrorList>> GetAllErrorListsAsync()
        {
            if (!await _errorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllErrorListsAsync] 錯誤清單 Repository 連線失敗");
            return await _errorListRep.GetAllWithTranslationsAsync().ConfigureAwait(false);
        }
        public async Task<List<ListType>> GetListTypesAsync()
        {
            if (!await _errorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetListTypesAsync] 錯誤清單 Repository 連線失敗");
            return await _errorListRep.GetListTypesAsync().ConfigureAwait(false);
        }

        public async Task AddErrorListAsync(ErrorListFormDto dto)
        {
            if (!await _errorListRep.CheckConnectionAsync().ConfigureAwait(false))
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
            await _errorListRep.AddErrorAsync(error, translations).ConfigureAwait(false);
        }

        public async Task UpdateErrorListAsync(ErrorListFormDto dto)
        {
            if (!await _errorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateErrorListAsync] 錯誤清單 Repository 連線失敗");
            await _errorListRep.UpdateErrorListAsync(dto).ConfigureAwait(false);
        }

        public async Task DeleteErrorListAsync(int id)
        {
            if (!await _errorListRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteErrorListAsync] 錯誤清單 Repository 連線失敗");
            var entity = await _errorListRep.GetByIdAsync(id).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[DeleteErrorListAsync] 找不到錯誤清單 ID={id}");
            await _errorListRep.DeleteErrorAsync(entity.ErrorCode).ConfigureAwait(false);
        }

        #endregion

        #region 設定：巡檢時段 CRUD 
        public async Task AddTimeSlotAsync(TimeSlotFormDto dto)
        {
            if (!await _timeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddTimeSlotAsync] 時段 Repository 連線失敗");
            var entity = new TimeSlotLookup
            {
                TimeSlotId = dto.TimeSlotId,
                StartAt = dto.StartAt,
                EndAt = dto.EndAt,
                IsCrossDay = dto.IsCrossDay,
                Label = dto.Label
            };
            await _timeSlotLookupRep.AddAsync(entity).ConfigureAwait(false);
        }
        public async Task UpdateTimeSlotAsync(TimeSlotFormDto dto)
        {
            if (!await _timeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateTimeSlotAsync] 時段 Repository 連線失敗");
            var entity = await _timeSlotLookupRep.GetByIdAsync(dto.TimeSlotId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[UpdateTimeSlotAsync] 找不到時段 ID={dto.TimeSlotId}");
            entity.StartAt = dto.StartAt;
            entity.EndAt = dto.EndAt;
            entity.IsCrossDay = dto.IsCrossDay;
            entity.Label = dto.Label;
            await _timeSlotLookupRep.UpdateAsync(entity).ConfigureAwait(false);
        }
        public async Task DeleteTimeSlotAsync(int timeSlotId)
        {
            if (!await _timeSlotLookupRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteTimeSlotAsync] 時段 Repository 連線失敗");
            await _timeSlotLookupRep.DeleteAsync(timeSlotId).ConfigureAwait(false);
        }

        #endregion

        #region 角色與權限 
        public async Task<List<Models.Role>> GetAllRolesAsync()
        {
            if (!await _rolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllRolesAsync] 角色權限 Repository 連線失敗");
            return await _rolePermissionRep.GetAllRolesAsync().ConfigureAwait(false);
        }

        #endregion

        #region 設定：角色權限 CRUD
        public async Task<List<Models.Permission>> GetAllPermissionsAsync()
        {
            if (!await _rolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllPermissionsAsync] 角色權限 Repository 連線失敗");
            return await _rolePermissionRep.GetAllPermissionsAsync().ConfigureAwait(false);
        }

        public async Task AddRoleAsync(RoleFormDto dto)
        {
            if (!await _rolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddRoleAsync] 角色權限 Repository 連線失敗");
            await _rolePermissionRep.AddRoleWithPermissionsAsync(dto.RoleId, dto.Name, dto.Description, dto.SelectedPermissionIds).ConfigureAwait(false);
        }

        public async Task UpdateRoleAsync(RoleFormDto dto)
        {
            if (!await _rolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateRoleAsync] 角色權限 Repository 連線失敗");
            await _rolePermissionRep.UpdateRoleWithPermissionsAsync(dto.Id!.Value, dto.Name, dto.Description, dto.SelectedPermissionIds).ConfigureAwait(false);
        }

        public async Task DeleteRoleAsync(int id)
        {
            if (!await _rolePermissionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteRoleAsync] 角色權限 Repository 連線失敗");
            if (await _rolePermissionRep.HasEmployeesByRoleAsync(id).ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteRoleAsync] 此角色有員工使用，無法刪除");
            await _rolePermissionRep.DeleteAsync(id).ConfigureAwait(false);
        }

        #endregion

        #region 設定：件號 CRUD

        public async Task<List<ProductPart>> GetAllProductPartsAsync()
        {
            if (!await _productPartRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllProductPartsAsync] 件號 Repository 連線失敗");
            return (await _productPartRep.GetAllAsync().ConfigureAwait(false)).ToList();
        }

        public async Task<int> AddProductPartAsync(ProductPartFormDto dto)
        {
            if (!await _productPartRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddProductPartAsync] 件號 Repository 連線失敗");
            var entity = new ProductPart
            {
                PartNo = dto.PartNo,
                Brand = dto.Brand,
                Name = dto.Name
            };
            await _productPartRep.AddAsync(entity).ConfigureAwait(false);
            return entity.PartId; // EF 回填 PK
        }

        #endregion

        #region 設定：SOP lookup

        public async Task<List<ProductModel>> GetProductModelsAsync()
        {
            if (!await _productRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetProductModelsAsync] 產品 Repository 連線失敗");
            return await _productRep.GetModelsAsync().ConfigureAwait(false);
        }

        public async Task<int> AddProductModelAsync(ProductModelFormDto dto)
        {
            if (!await _sopChecklistRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddProductModelAsync] SOP Repository 連線失敗");
            return await _sopChecklistRep.AddProductModelAsync(dto.Name, dto.Remark).ConfigureAwait(false);
        }

        public async Task<List<WorkProcess>> GetWorkProcessesAsync()
        {
            if (!await _sopChecklistRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetWorkProcessesAsync] SOP Repository 連線失敗");
            return await _sopChecklistRep.GetProcessesAsync().ConfigureAwait(false);
        }

        public async Task<List<Material>> GetMaterialsByTypeAsync(int typeId)
        {
            if (!await _materialRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetMaterialsByTypeAsync] 物料 Repository 連線失敗");
            var all = await _materialRep.GetAllAsync().ConfigureAwait(false);
            return all.Where(m => m.TypeId == typeId).ToList();
        }

        #endregion

        #region 設定：SOP 點檢表 CRUD

        public async Task<List<SopChecklist>> GetAllSopChecklistsAsync()
        {
            if (!await _sopChecklistRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllSopChecklistsAsync] SOP Repository 連線失敗");
            await using var ctx = _mesFactory.CreateDbContext();
            return await ctx.SopChecklists
                .Include(s => s.Product).ThenInclude(p => p!.Part)
                .Include(s => s.Product).ThenInclude(p => p!.Model)
                .Include(s => s.Process)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<SopChecklist?> GetSopChecklistWithItemsAsync(int sopId)
        {
            if (!await _sopChecklistRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetSopChecklistWithItemsAsync] SOP Repository 連線失敗");
            return await _sopChecklistRep.GetWithItemsAsync(sopId).ConfigureAwait(false);
        }

        public async Task AddSopChecklistAsync(SopChecklistFormDto dto)
        {
            if (!await _sopChecklistRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddSopChecklistAsync] SOP Repository 連線失敗");

            await using var ctx = _mesFactory.CreateDbContext();
            var productId = await EnsureProductAsync(ctx, dto.PartId, dto.ModelId).ConfigureAwait(false);

            var entity = new SopChecklist
            {
                ProductId = productId,
                ProcessId = dto.ProcessId,
                SopType = dto.SopType,
                Remark = dto.Remark
            };
            foreach (var i in dto.Items)
            {
                entity.Items.Add(new SopChecklistItem
                {
                    Seq = i.Seq,
                    CheckType = i.CheckType,
                    WorkstationNo = i.WorkstationNo,
                    MaterialId = i.MaterialId,
                    Quantity = i.Quantity,
                    Content = i.Content,
                    Remark = i.Remark
                });
            }
            ctx.SopChecklists.Add(entity);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task UpdateSopChecklistAsync(SopChecklistFormDto dto)
        {
            if (!await _sopChecklistRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateSopChecklistAsync] SOP Repository 連線失敗");
            if (dto.Id == null)
                throw new InvalidOperationException("[UpdateSopChecklistAsync] dto.Id 不可為空");

            await using var ctx = _mesFactory.CreateDbContext();

            var existing = await ctx.SopChecklists
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.SopId == dto.Id.Value)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[UpdateSopChecklistAsync] 找不到 SOP ID={dto.Id}");

            var productId = await EnsureProductAsync(ctx, dto.PartId, dto.ModelId).ConfigureAwait(false);
            existing.ProductId = productId;
            existing.ProcessId = dto.ProcessId;
            existing.SopType = dto.SopType;
            existing.Remark = dto.Remark;
            existing.UpdateAt = DateTime.Now;

            // Items 差異
            var dtoItemIds = dto.Items.Where(i => i.Id != null).Select(i => i.Id!.Value).ToHashSet();
            var toRemove = existing.Items.Where(i => !dtoItemIds.Contains(i.ItemId)).ToList();
            foreach (var item in toRemove)
                ctx.SopChecklistItems.Remove(item);

            foreach (var i in dto.Items)
            {
                if (i.Id == null)
                {
                    existing.Items.Add(new SopChecklistItem
                    {
                        Seq = i.Seq,
                        CheckType = i.CheckType,
                        WorkstationNo = i.WorkstationNo,
                        MaterialId = i.MaterialId,
                        Quantity = i.Quantity,
                        Content = i.Content,
                        Remark = i.Remark
                    });
                }
                else
                {
                    var target = existing.Items.FirstOrDefault(x => x.ItemId == i.Id.Value);
                    if (target == null) continue;
                    target.Seq = i.Seq;
                    target.CheckType = i.CheckType;
                    target.WorkstationNo = i.WorkstationNo;
                    target.MaterialId = i.MaterialId;
                    target.Quantity = i.Quantity;
                    target.Content = i.Content;
                    target.Remark = i.Remark;
                }
            }

            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task DeleteSopChecklistAsync(int id)
        {
            if (!await _sopChecklistRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteSopChecklistAsync] SOP Repository 連線失敗");
            await _sopChecklistRep.DeleteAsync(id).ConfigureAwait(false);
        }

        // 取或建 Product：UI 不暴露 Product；EnsureProductAsync 由 SOP 寫入時自動處理
        private static async Task<int> EnsureProductAsync(MesDbContext ctx, int partId, int modelId)
        {
            var existing = await ctx.Products
                .FirstOrDefaultAsync(p => p.PartId == partId && p.ModelId == modelId)
                .ConfigureAwait(false);
            if (existing != null) return existing.ProductId;

            var p = new Product { PartId = partId, ModelId = modelId };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return p.ProductId;
        }

        #endregion

        #region 設定：機台可生產清單 CRUD

        public async Task<List<EquipmentProduct>> GetAllEquipmentProductsAsync()
        {
            if (!await _equipmentProductRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllEquipmentProductsAsync] 機台可生產清單 Repository 連線失敗");
            await using var ctx = _mesFactory.CreateDbContext();
            return await ctx.EquipmentProducts
                .Include(ep => ep.Equipment)
                .Include(ep => ep.Sop).ThenInclude(s => s!.Product).ThenInclude(p => p!.Part)
                .Include(ep => ep.Sop).ThenInclude(s => s!.Product).ThenInclude(p => p!.Model)
                .Include(ep => ep.Sop).ThenInclude(s => s!.Process)
                .OrderBy(ep => ep.EquipmentId).ThenBy(ep => ep.SeqNo)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<EquipmentProduct>> GetEquipmentProductsByEquipmentAsync(int equipmentId)
        {
            if (!await _equipmentProductRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetEquipmentProductsByEquipmentAsync] 機台可生產清單 Repository 連線失敗");
            await using var ctx = _mesFactory.CreateDbContext();
            return await ctx.EquipmentProducts
                .Where(ep => ep.EquipmentId == equipmentId)
                .Include(ep => ep.Sop).ThenInclude(s => s!.Product).ThenInclude(p => p!.Part)
                .Include(ep => ep.Sop).ThenInclude(s => s!.Product).ThenInclude(p => p!.Model)
                .Include(ep => ep.Sop).ThenInclude(s => s!.Process)
                .OrderBy(ep => ep.SeqNo)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task AddEquipmentProductAsync(EquipmentProductFormDto dto)
        {
            if (!await _equipmentProductRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddEquipmentProductAsync] 機台可生產清單 Repository 連線失敗");
            var entity = new EquipmentProduct
            {
                EquipmentId = dto.EquipmentId,
                SopId = dto.SopId,
                SeqNo = dto.SeqNo,
                ProductionStatus = dto.ProductionStatus
            };
            await _equipmentProductRep.AddAsync(entity).ConfigureAwait(false);
        }

        public async Task UpdateEquipmentProductAsync(EquipmentProductFormDto dto)
        {
            if (!await _equipmentProductRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateEquipmentProductAsync] 機台可生產清單 Repository 連線失敗");
            var entity = await _equipmentProductRep.GetByIdAsync(dto.Id!.Value).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[UpdateEquipmentProductAsync] 找不到 EquipmentProduct ID={dto.Id}");
            entity.SopId = dto.SopId;
            entity.SeqNo = dto.SeqNo;
            entity.ProductionStatus = dto.ProductionStatus;
            entity.UpdateAt = DateTime.Now;
            await _equipmentProductRep.UpdateAsync(entity).ConfigureAwait(false);
        }

        public async Task<DateTime> UpdateProductionStatusAsync(int equipmentProductId, TuningType newStatus)
        {
            if (!await _equipmentProductRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateProductionStatusAsync] 機台可生產清單 Repository 連線失敗");
            var entity = await _equipmentProductRep.GetByIdAsync(equipmentProductId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"[UpdateProductionStatusAsync] 找不到 EquipmentProduct ID={equipmentProductId}");
            entity.ProductionStatus = newStatus;
            entity.UpdateAt = DateTime.Now;
            await _equipmentProductRep.UpdateAsync(entity).ConfigureAwait(false);
            return entity.UpdateAt.Value;
        }

        public async Task DeleteEquipmentProductAsync(int id)
        {
            if (!await _equipmentProductRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteEquipmentProductAsync] 機台可生產清單 Repository 連線失敗");
            await _equipmentProductRep.DeleteAsync(id).ConfigureAwait(false);
        }

        #endregion

        #region 接單服務

        public async Task<List<OrderProduction>> GetAllOrderProductionsAsync()
        {
            if (!await _orderProductionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllOrderProductionsAsync] 接單 Repository 連線失敗");
            return await _orderProductionRep.GetAllAsync().ConfigureAwait(false);
        }

        public async Task<List<OrderProduction>> GetOrdersByEquipmentAsync(int equipmentId)
        {
            if (!await _orderProductionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetOrdersByEquipmentAsync] 接單 Repository 連線失敗");
            return await _orderProductionRep.GetByEquipmentAsync(equipmentId).ConfigureAwait(false);
        }

        public async Task<List<OrderProduction>> GetOrdersByScheduleAsync(int scheduleId)
        {
            if (!await _orderProductionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetOrdersByScheduleAsync] 接單 Repository 連線失敗");
            return await _orderProductionRep.GetByScheduleAsync(scheduleId).ConfigureAwait(false);
        }

        public async Task<OrderProduction?> GetInProductionOrderAsync(int equipmentId)
        {
            if (!await _orderProductionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetInProductionOrderAsync] 接單 Repository 連線失敗");
            return await _orderProductionRep.GetInProductionByEquipmentAsync(equipmentId).ConfigureAwait(false);
        }

        public async Task<int> AddOrderAsync(int equipmentId, int equipmentProductId, int? quantity, int createdBy, int? scheduleId = null)
        {
            if (!await _orderProductionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddOrderAsync] 接單 Repository 連線失敗");
            var orderId = await _orderProductionRep.AddAsync(equipmentId, equipmentProductId, quantity, createdBy, scheduleId).ConfigureAwait(false);
            if (scheduleId.HasValue)
            {
                var schedule = await _scheduleRep.GetByIdWithDetailsAsync(scheduleId.Value).ConfigureAwait(false);
                if (schedule?.Status == ScheduleStatus.Pending)
                    await _scheduleRep.MarkScheduledAsync(scheduleId.Value, createdBy, DateTime.Now).ConfigureAwait(false);
                await _scheduleRep.RecalcActualQuantityAsync(scheduleId.Value).ConfigureAwait(false);
            }
            return orderId;
        }

        public async Task StartProductionAsync(int orderId, int startedBy)
        {
            if (!await _orderProductionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[StartProductionAsync] 接單 Repository 連線失敗");
            await _orderProductionRep.StartAsync(orderId, startedBy, DateTime.Now).ConfigureAwait(false);
        }

        public async Task EndProductionAsync(int orderId)
        {
            if (!await _orderProductionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[EndProductionAsync] 接單 Repository 連線失敗");
            await _orderProductionRep.EndAsync(orderId, DateTime.Now).ConfigureAwait(false);
        }

        public async Task CancelOrderAsync(int orderId, string? description)
        {
            if (!await _orderProductionRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[CancelOrderAsync] 接單 Repository 連線失敗");
            var order = await _orderProductionRep.GetByIdAsync(orderId).ConfigureAwait(false);
            await _orderProductionRep.CancelAsync(orderId, description).ConfigureAwait(false);
            if (order?.ScheduleId.HasValue == true)
                await _scheduleRep.RecalcActualQuantityAsync(order.ScheduleId.Value).ConfigureAwait(false);
        }

        #endregion

        #region 調試服務

        public async Task<int> StartProgramTuningAsync(int equipmentId, int equipmentProductId, TuningType type, int startedBy, DateTime startedAt)
        {
            if (type != TuningType.Teaching && type != TuningType.Offset)
                throw new ArgumentException($"[StartProgramTuningAsync] 不支援的調試模式 type={type}");
            if (!await _programTuningRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[StartProgramTuningAsync] 調試 Repository 連線失敗");
            return await _programTuningRep.StartAsync(equipmentId, equipmentProductId, type, startedBy, startedAt).ConfigureAwait(false);
        }

        public async Task EndProgramTuningAsync(int programTuningId, DateTime endedAt, string? description = null)
        {
            if (!await _programTuningRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[EndProgramTuningAsync] 調試 Repository 連線失敗");
            await _programTuningRep.EndAsync(programTuningId, endedAt, description).ConfigureAwait(false);
        }

        public async Task<ProgramTuningRecord?> GetInProgressProgramTuningAsync(int equipmentId)
        {
            if (!await _programTuningRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetInProgressProgramTuningAsync] 調試 Repository 連線失敗");
            return await _programTuningRep.GetInProgressByEquipmentAsync(equipmentId).ConfigureAwait(false);
        }

        public async Task<List<ProgramTuningRecord>> GetAllInProgressProgramTuningAsync()
        {
            if (!await _programTuningRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllInProgressProgramTuningAsync] 調試 Repository 連線失敗");
            return await _programTuningRep.GetAllInProgressAsync().ConfigureAwait(false);
        }

        #endregion

        #region MESData：製程資料 View

        public async Task<IEnumerable<VwMesDailyProcessData>> GetDailyProcessDataAsync()
            => await _dataRep.GetDailyProcessDataAsync().ConfigureAwait(false);

        #endregion

        #region MESInformation：客戶代碼 & MES 設備

        public async Task<string?> GetCustomerByCodeAsync(string code)
        {
            var medium = code.Substring(2, 2);
            return await _infoRep.GetCustomerByMediumAsync(medium).ConfigureAwait(false);
        }

        public async Task<IEnumerable<MesDevice>> GetAllMesDevicesAsync()
        {
            return await _infoRep.GetAllMesDevicesAsync().ConfigureAwait(false);
        }

        public async Task AddMesDeviceAsync(MesDevice entity)
        {
            await _infoRep.AddMesDeviceAsync(entity).ConfigureAwait(false);
        }

        public async Task UpdateMesDeviceAsync(MesDevice entity)
        {
            await _infoRep.UpdateMesDeviceAsync(entity).ConfigureAwait(false);
        }

        #endregion

        #region 出入料管理：排程服務

        public async Task<int> AddScheduleAsync(ScheduleCreateDto dto)
        {
            if (!await _scheduleRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddScheduleAsync] 排程 Repository 連線失敗");
            var entity = new Schedule
            {
                ProductId   = dto.ProductId,
                ProcessId   = dto.ProcessId,
                Quantity    = dto.Quantity,
                LotNo       = dto.LotNo,
                Status      = ScheduleStatus.Pending,
                ReceivedBy  = dto.ReceivedBy,
                ReceivedAt  = DateTime.Now,
                Description = dto.Description,
                CreateAt    = DateTime.Now,
                UpdateAt    = DateTime.Now
            };
            return await _scheduleRep.AddAsync(entity).ConfigureAwait(false);
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            if (!await _productRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllProductsAsync] 產品 Repository 連線失敗");
            return await _productRep.GetAllWithDetailsAsync().ConfigureAwait(false);
        }

        public async Task<List<Schedule>> GetAllSchedulesAsync()
        {
            if (!await _scheduleRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetAllSchedulesAsync] 排程 Repository 連線失敗");
            return await _scheduleRep.GetAllWithDetailsAsync().ConfigureAwait(false);
        }

        public async Task<Schedule?> GetScheduleByIdAsync(int id)
        {
            if (!await _scheduleRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetScheduleByIdAsync] 排程 Repository 連線失敗");
            return await _scheduleRep.GetByIdWithDetailsAsync(id).ConfigureAwait(false);
        }

        public async Task MarkScheduledAsync(int scheduleId, int employeeId)
        {
            if (!await _scheduleRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[MarkScheduledAsync] 排程 Repository 連線失敗");
            await _scheduleRep.MarkScheduledAsync(scheduleId, employeeId, DateTime.Now).ConfigureAwait(false);
        }

        public async Task MarkVerifiedAsync(int scheduleId, int employeeId, int? actualQty, string? description)
        {
            if (!await _scheduleRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[MarkVerifiedAsync] 排程 Repository 連線失敗");
            await _scheduleRep.MarkVerifiedAsync(scheduleId, employeeId, DateTime.Now, actualQty, description).ConfigureAwait(false);
        }

        public async Task MarkReleasedAsync(int scheduleId, int employeeId, string? description)
        {
            if (!await _scheduleRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[MarkReleasedAsync] 排程 Repository 連線失敗");
            await _scheduleRep.MarkReleasedAsync(scheduleId, employeeId, DateTime.Now, description).ConfigureAwait(false);
        }

        public async Task CancelScheduleAsync(int scheduleId, string? description)
        {
            if (!await _scheduleRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[CancelScheduleAsync] 排程 Repository 連線失敗");
            await _scheduleRep.MarkCancelledAsync(scheduleId, description).ConfigureAwait(false);
        }

        public async Task ForceCompleteAsync(int scheduleId, int employeeId, int? actualQty, string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new BusinessRuleException("[ForceCompleteAsync] 強制完成必須填寫說明");
            if (!await _scheduleRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[ForceCompleteAsync] 排程 Repository 連線失敗");
            await _scheduleRep.ForceCompleteAsync(scheduleId, employeeId, DateTime.Now, actualQty, description)
                .ConfigureAwait(false);
        }

        public async Task SplitScheduleAsync(ScheduleSplitDto dto)
        {
            if (dto.RemainingQuantity <= 0)
                throw new BusinessRuleException("[SplitScheduleAsync] remainingQuantity 必須大於 0");
            if (!await _scheduleRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[SplitScheduleAsync] 排程 Repository 連線失敗");
            await _scheduleRep.SplitScheduleAsync(
                dto.OriginalScheduleId, dto.RemainingQuantity, dto.ReleasedBy, DateTime.Now, dto.Description)
                .ConfigureAwait(false);
        }

        #endregion
    }
}
