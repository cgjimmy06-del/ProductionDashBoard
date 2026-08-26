using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services.Exceptions;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// <see cref="IWarehouseService"/> 的實作（服務定位與跨域查詢說明見介面 doc）。
    /// 獨立於 DataService 的倉儲領域服務，經 Facade <see cref="DashboardCoreServices.Warehouse"/> 對外；
    /// 比照 DataService 的三段式錯誤處理慣例（連線守衛 → BusinessRuleException 規則檢查 →
    /// SqlException/TimeoutException 包成 DatabaseConnectionException）。
    /// </summary>
    public class WarehouseService : IWarehouseService
    {
        private readonly IWarehouseRepository _warehouseRep;

        public WarehouseService(IWarehouseRepository warehouseRep)
        {
            _warehouseRep = warehouseRep;
        }

        public async Task<IEnumerable<StorageLocation>> GetLocationsAsync()
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetLocationsAsync] 連線失敗，請確認網路狀態");
            try
            {
                return await _warehouseRep.GetAllAsync().ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[GetLocationsAsync] 取得倉位清單失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[GetLocationsAsync] 取得倉位清單失敗：連線逾時", tex); }
        }

        public async Task<StorageLocation?> GetLocationByIdAsync(int locationId)
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetLocationByIdAsync] 連線失敗，請確認網路狀態");
            try
            {
                return await _warehouseRep.GetByIdAsync(locationId).ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[GetLocationByIdAsync] 取得倉位失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[GetLocationByIdAsync] 取得倉位失敗：連線逾時", tex); }
        }

        public async Task AddLocationAsync(StorageLocation location)
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AddLocationAsync] 連線失敗，請確認網路狀態");

            if (await _warehouseRep.ExistsLocationCodeAsync(location.Code).ConfigureAwait(false))
                throw new BusinessRuleException($"[AddLocationAsync] 倉位編號已存在：{location.Code}");

            try
            {
                await _warehouseRep.AddAsync(location).ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[AddLocationAsync] 新增倉位失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[AddLocationAsync] 新增倉位失敗：連線逾時", tex); }
        }

        public async Task UpdateLocationAsync(StorageLocation location)
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[UpdateLocationAsync] 連線失敗，請確認網路狀態");

            if (await _warehouseRep.ExistsLocationCodeAsync(location.Code, location.LocationId).ConfigureAwait(false))
                throw new BusinessRuleException($"[UpdateLocationAsync] 倉位編號已存在：{location.Code}");

            location.UpdateAt = DateTime.Now;
            try
            {
                await _warehouseRep.UpdateAsync(location).ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[UpdateLocationAsync] 更新倉位失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[UpdateLocationAsync] 更新倉位失敗：連線逾時", tex); }
        }

        public async Task DeleteLocationAsync(int locationId)
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[DeleteLocationAsync] 連線失敗，請確認網路狀態");

            if (await _warehouseRep.HasActiveAssignmentsAtLocationAsync(locationId).ConfigureAwait(false))
                throw new BusinessRuleException("[DeleteLocationAsync] 倉位尚有現役佔用，無法刪除");

            try
            {
                await _warehouseRep.DeleteAsync(locationId).ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[DeleteLocationAsync] 刪除倉位失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[DeleteLocationAsync] 刪除倉位失敗：連線逾時", tex); }
        }

        public async Task<int> AssignAsync(int locationId, int scheduleId, int operatorId)
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[AssignAsync] 連線失敗，請確認網路狀態");

            if (await _warehouseRep.HasActiveAssignmentAsync(scheduleId).ConfigureAwait(false))
                throw new BusinessRuleException($"[AssignAsync] 此箱已在倉位上架中 ScheduleId={scheduleId}");

            try
            {
                // DB filtered unique index 為併發後盾：若通過上方檢查後仍被搶先，第二筆 insert 會在 DB 端失敗
                return await _warehouseRep.AssignAsync(locationId, scheduleId, operatorId).ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[AssignAsync] 上架失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[AssignAsync] 上架失敗：連線逾時", tex); }
        }

        public async Task ReleaseAsync(int scheduleId, int operatorId)
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[ReleaseAsync] 連線失敗，請確認網路狀態");
            try
            {
                await _warehouseRep.ReleaseAsync(scheduleId, operatorId).ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[ReleaseAsync] 下架失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[ReleaseAsync] 下架失敗：連線逾時", tex); }
        }

        public async Task ReassignAsync(int scheduleId, int newLocationId, int operatorId)
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[ReassignAsync] 連線失敗，請確認網路狀態");
            try
            {
                await _warehouseRep.ReassignAsync(scheduleId, newLocationId, operatorId).ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[ReassignAsync] 換倉失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[ReassignAsync] 換倉失敗：連線逾時", tex); }
        }

        public async Task<List<LocationAssignment>> GetActiveAssignmentsAsync(int locationId)
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetActiveAssignmentsAsync] 連線失敗，請確認網路狀態");
            try
            {
                return await _warehouseRep.GetActiveAssignmentsAsync(locationId).ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[GetActiveAssignmentsAsync] 取得佔用清單失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[GetActiveAssignmentsAsync] 取得佔用清單失敗：連線逾時", tex); }
        }

        public async Task<Dictionary<int, StorageLocation>> GetActiveAssignmentMapAsync()
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetActiveAssignmentMapAsync] 連線失敗，請確認網路狀態");
            try
            {
                var actives = await _warehouseRep.GetAllActiveAssignmentsAsync().ConfigureAwait(false);
                var map = new Dictionary<int, StorageLocation>();
                foreach (var a in actives)
                    if (a.Location != null)
                        map[a.ScheduleId] = a.Location;   // filtered unique index 保證每 schedule 至多一筆現役，無鍵衝突
                return map;
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[GetActiveAssignmentMapAsync] 取得佔用對照失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[GetActiveAssignmentMapAsync] 取得佔用對照失敗：連線逾時", tex); }
        }

        public async Task<Dictionary<int, int>> GetOccupancyCountsAsync()
        {
            if (!await _warehouseRep.CheckConnectionAsync().ConfigureAwait(false))
                throw new InvalidOperationException("[GetOccupancyCountsAsync] 連線失敗，請確認網路狀態");
            try
            {
                return await _warehouseRep.GetActiveCountByLocationAsync().ConfigureAwait(false);
            }
            catch (SqlException ex) { throw new DatabaseConnectionException("[GetOccupancyCountsAsync] 取得佔用計數失敗：資料庫錯誤", ex); }
            catch (TimeoutException tex) { throw new DatabaseConnectionException("[GetOccupancyCountsAsync] 取得佔用計數失敗：連線逾時", tex); }
        }
    }
}
