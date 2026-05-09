using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.UiModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
    public interface IDataService
    {
        public DateTime BusinessDay { get; set; }
#if DEBUG
        public Task Demo();
#endif
        public Task<List<DeviceInfo>> GetDevicesAsync();
        public Task<List<UserInfo>> GetUsersAsync();
        public Task<List<MaterialInfo>> GetMaterialsAsync();
        public Task<List<ErrorInfo>> GetErrorsAsync(string languageCode);
        public Task<List<TimeSlotLookup>> GetTimeSlotsAsync();
        /// <summary>
        /// 新增物料更換紀錄 (支援離線暫存)
        /// </summary>
        public Task<int> AddReplacementRecordAsync(int equipmentId, int employeeId, List<(int materialId, int quantity)> materialDetails);
        /// <summary>
        /// 取得當前巡檢區段ID
        /// </summary>
        public Task<int?> GetCurrentTimeSlotIdAsync();
        /// <summary>
        /// 取得當前巡檢區段ID (離線表)
        /// </summary>
        public int? GetCurrentTimeSlotId(List<TimeSlotLookup> timeslots);
        /// <summary>
        /// 新增首件紀錄 (支援離線暫存)
        /// </summary>
        public Task<int> AddFirstInspectionAsync(int equipmentId, int employeeId, bool result,
            string? product, string? errorCode = null, string? description = null);
        /// <summary>
        /// 新增巡檢紀錄 (支援離線暫存)
        /// </summary>
        public Task<int> AddRoutineInspectionAsync(int equipmentId, int employeeId, bool result,
            int timeSlotId, string? product, string? errorCode = null, string? description = null);
        /// <summary>
        /// 檢查某設備在每個時段的狀態 (TimeSlotStatus)
        /// </summary>
        public Task<List<int>> GetAllSlotsStatusAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId);
        /// <summary>
        /// 檢查當前時段是否有巡檢紀錄，若沒有則補一筆「未巡檢」紀錄 (不可離線暫存，若進到下個工作日仍無法上傳則不能更新未巡檢紀錄)
        /// </summary>
        public Task CheckAndInsertMissedInspectionAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId);
        /// <summary>
        /// 新增帶點紀錄 (支援離線暫存)
        /// </summary>
        public Task<int> AddTeachingRecordAsync(int equipmentId, int employeeId, int durationSec, string? product);
        /// <summary>
        /// 新增調品質紀錄 (支援離線暫存)
        /// </summary>
        public Task<int> AddOffsetRecordAsync(int equipmentId, int employeeId, int durationSec, string? product);

        // ─── 設定：設備 CRUD ─────────────────────────────────────────────────────────
        public Task<List<Equipment>> GetAllEquipmentAsync();
        public Task AddEquipmentAsync(EquipmentFormDto dto);
        public Task UpdateEquipmentAsync(EquipmentFormDto dto);
        public Task DeleteEquipmentAsync(int id);
        public Task<List<EquipmentType>> GetEquipmentTypesAsync();

        // ─── 設定：員工 CRUD ─────────────────────────────────────────────────────────
        public Task<List<Employee>> GetAllEmployeesAsync();
        public Task AddEmployeeAsync(EmployeeFormDto dto);
        public Task UpdateEmployeeAsync(EmployeeFormDto dto);     // dto.Password 空字串 = 不更新密碼
        public Task DeleteEmployeeAsync(int id);

        // ─── 設定：材料 CRUD ─────────────────────────────────────────────────────────
        public Task<List<Material>> GetAllMaterialsAsync();
        public Task<List<MaterialType>> GetMaterialTypesAsync();
        public Task AddMaterialAsync(MaterialFormDto dto);
        public Task UpdateMaterialAsync(MaterialFormDto dto);
        public Task DeleteMaterialAsync(int id);

        // ─── 設定：錯誤清單 CRUD ─────────────────────────────────────────────────────
        public Task<List<ErrorList>> GetAllErrorListsAsync();
        public Task<List<ListType>> GetListTypesAsync();
        public Task AddErrorListAsync(ErrorListFormDto dto);
        public Task UpdateErrorListAsync(ErrorListFormDto dto);
        public Task DeleteErrorListAsync(int id);

        // ─── 設定：巡檢時段 CRUD ─────────────────────────────────────────────────
        public Task AddTimeSlotAsync(TimeSlotFormDto dto);
        public Task UpdateTimeSlotAsync(TimeSlotFormDto dto);
        public Task DeleteTimeSlotAsync(int timeSlotId);

        // ─── 角色與權限 ───────────────────────────────────────────────────────────
        public Task<List<Models.Role>> GetAllRolesAsync();

        // ─── 設定：角色權限 CRUD ─────────────────────────────────────────────────
        public Task<List<Models.Permission>> GetAllPermissionsAsync();
        public Task AddRoleAsync(RoleFormDto dto);
        public Task UpdateRoleAsync(RoleFormDto dto);
        public Task DeleteRoleAsync(int id);
    }
}
