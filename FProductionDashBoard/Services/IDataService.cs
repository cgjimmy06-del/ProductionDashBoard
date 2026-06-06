using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Models.Extra;
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
            int? productId, string? errorCode = null, string? description = null);
        /// <summary>
        /// 新增巡檢紀錄 (支援離線暫存)
        /// </summary>
        public Task<int> AddRoutineInspectionAsync(int equipmentId, int employeeId, bool result,
            int timeSlotId, int? productId, string? errorCode = null, string? description = null);
        /// <summary>
        /// 檢查某設備在每個時段的狀態 (TimeSlotStatus)
        /// </summary>
        public Task<List<int>> GetAllSlotsStatusAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId);
        /// <summary>
        /// 檢查當前時段是否有巡檢紀錄，若沒有則補一筆「未巡檢」紀錄 (不可離線暫存，若進到下個工作日仍無法上傳則不能更新未巡檢紀錄)
        /// </summary>
        public Task CheckAndInsertMissedInspectionAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId);
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

        // ─── 設定：件號 CRUD（SOP 點檢表暴露給 Part 篩選 + inline 新增） ──────
        public Task<List<ProductPart>> GetAllProductPartsAsync();
        public Task<int> AddProductPartAsync(ProductPartFormDto dto);

        // ─── 設定：SOP lookup（下拉用） ─────────────────────────────────────
        public Task<List<ProductModel>> GetProductModelsAsync();
        public Task<int> AddProductModelAsync(ProductModelFormDto dto);
        public Task<List<WorkProcess>> GetWorkProcessesAsync();
        public Task<List<Material>> GetMaterialsByTypeAsync(int typeId);

        // ─── 設定：SOP 點檢表 CRUD ─────────────────────────────────────────
        public Task<List<SopChecklist>> GetAllSopChecklistsAsync();
        public Task<SopChecklist?> GetSopChecklistWithItemsAsync(int sopId);
        public Task AddSopChecklistAsync(SopChecklistFormDto dto);
        public Task UpdateSopChecklistAsync(SopChecklistFormDto dto);
        public Task DeleteSopChecklistAsync(int id);

        // ─── 設定：機台可生產清單 CRUD ─────────────────────────────────────
        public Task<List<EquipmentProduct>> GetAllEquipmentProductsAsync();
        public Task<List<EquipmentProduct>> GetEquipmentProductsByEquipmentAsync(int equipmentId);
        public Task AddEquipmentProductAsync(EquipmentProductFormDto dto);
        public Task UpdateEquipmentProductAsync(EquipmentProductFormDto dto);
        public Task<DateTime> UpdateProductionStatusAsync(int equipmentProductId, TuningType newStatus);
        public Task DeleteEquipmentProductAsync(int id);

        // ─── 接單服務 ─────────────────────────────────────────────────────────
        public Task<List<OrderProduction>> GetOrdersByEquipmentAsync(int equipmentId);
        public Task<OrderProduction?> GetInProductionOrderAsync(int equipmentId);
        public Task<int> AddOrderAsync(int equipmentId, int equipmentProductId, int? quantity, int createdBy);
        public Task StartProductionAsync(int orderId, int startedBy);
        public Task EndProductionAsync(int orderId);
        public Task CancelOrderAsync(int orderId, string? description);

        // ─── 調試服務 ─────────────────────────────────────────────────────────
        public Task<int> StartProgramTuningAsync(int equipmentId, int equipmentProductId, TuningType type, int startedBy, DateTime startedAt);
        public Task EndProgramTuningAsync(int programTuningId, DateTime endedAt, string? description = null);
        public Task<ProgramTuningRecord?> GetInProgressProgramTuningAsync(int equipmentId);

        // ─── MESInformation：客戶代碼 & MES 設備 ────────────────────────────
        /// <summary>
        /// 輸入固定 8 碼字串，取第 3、4 碼比對 Medium_categories，回傳第一筆 Customer（無符合則 null）
        /// </summary>
        public Task<string?> GetCustomerByCodeAsync(string code);
        public Task<IEnumerable<MesDevice>> GetAllMesDevicesAsync();
        public Task AddMesDeviceAsync(MesDevice entity);
        public Task UpdateMesDeviceAsync(MesDevice entity);

        // ─── MESData：製程資料 View ──────────────────────────────────────────
        public Task<IEnumerable<VwMesDailyProcessData>> GetDailyProcessDataAsync();

        // ─── 出入料管理：排程服務 ────────────────────────────────────────────
        public Task<int> AddScheduleAsync(ScheduleCreateDto dto);
        public Task<List<Schedule>> GetAllSchedulesAsync();
        public Task<Schedule?> GetScheduleByIdAsync(int id);
        public Task MarkScheduledAsync(int scheduleId, int employeeId);
        public Task MarkVerifiedAsync(int scheduleId, int employeeId, int? actualQty, string? description);
        public Task MarkReleasedAsync(int scheduleId, int employeeId, string? description);
        public Task CancelScheduleAsync(int scheduleId, string? description);
        public Task ForceCompleteAsync(int scheduleId, int employeeId, int? actualQty, string description);
        public Task SplitScheduleAsync(ScheduleSplitDto dto);
    }
}
