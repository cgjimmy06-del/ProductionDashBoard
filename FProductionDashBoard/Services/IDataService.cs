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
        public Task Demo();
        public Task<List<DeviceInfo>> GetDevicesAsync();
        public Task<List<UserInfo>> GetUsersAsync();
        public Task<List<MaterialInfo>> GetMaterialsAsync();
        public Task<List<ErrorInfo>> GetErrorsAsync(string languageCode);
        public Task<List<TimeSlotLookup>> GetTimeSlotsAsync();
        public IEquipmentRepository EquipmentRep { get; }
        public IEmployeeRepository EmployeeRep { get; }
        public IMaterialRepository MaterialRep { get; }
        public IErrorListRepository ErrorListRep { get; }
        public IMaterialReplacementRepository MaterialReplacementRep { get; }
        public ITimeSlotLookupRepository TimeSlotLookupRep { get; }
        public IInspectionRecordRepository InspectionRecordRep { get; }




        /// <summary>
        /// 新增物料更換紀錄
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
        /// 新增首件紀錄
        /// </summary>
        public Task<int> AddFirstInspectionAsync(int equipmentId, int employeeId, bool result,
            string? product, string? errorCode = null, string? description = null);
        /// <summary>
        /// 新增巡檢紀錄
        /// </summary>
        public Task<int> AddRoutineInspectionAsync(int equipmentId, int employeeId, bool result,
            int timeSlotId, string? product, string? errorCode = null, string? description = null);
        /// <summary>
        /// 檢查某設備在每個時段的狀態 (TimeSlotStatus)
        /// </summary>
        public Task<List<int>> GetAllSlotsStatusAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId);
        /// <summary>
        /// 檢查當前時段是否有巡檢紀錄，若沒有則補一筆「未巡檢」紀錄
        /// </summary>
        public Task CheckAndInsertMissedInspectionAsync(List<TimeSlotLookup> timeSlotLookups, int equipmentId);

    }
}
