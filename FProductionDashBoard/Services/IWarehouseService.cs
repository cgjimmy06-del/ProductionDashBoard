using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 倉儲業務服務：倉位主檔 CRUD、上架/下架生命週期、佔用查詢。
    ///
    /// 【與 DataService 的關係】本服務是從 <see cref="IDataService"/> 抽出的第一個獨立領域服務——
    /// IDataService 已逾 160 行、DataService 建構子注入 18+ 個 repo，倉儲領域不再灌入其中，改以獨立
    /// IWarehouseService 分區承載。惟兩者共用同一個 MesDbContext（經 <c>IWarehouseRepository</c> 的
    /// <c>IDbContextFactory</c>），故仍可跨域 join 到 schedule / product / equipment 等既有資料表
    /// （見 <see cref="GetActiveAssignmentsAsync"/>）。「服務獨立」只是門面分區，底層資料模型仍共用。
    ///
    /// 【取用方式】與 Data / Authorization / CardReader 同級，統一經 Facade
    /// <see cref="DashboardCoreServices.Warehouse"/> 供 ViewModel 取用（不直接注入各 ViewModel）。
    /// </summary>
    public interface IWarehouseService
    {
        Task<IEnumerable<StorageLocation>> GetLocationsAsync();
        Task<StorageLocation?> GetLocationByIdAsync(int locationId);
        Task AddLocationAsync(StorageLocation location);
        Task UpdateLocationAsync(StorageLocation location);
        Task DeleteLocationAsync(int locationId);
        Task<int> AssignAsync(int locationId, int scheduleId, int operatorId);
        Task ReleaseAsync(int scheduleId, int operatorId);
        Task<List<LocationAssignment>> GetActiveAssignmentsAsync(int locationId);
        Task<Dictionary<int, int>> GetOccupancyCountsAsync();
    }
}
