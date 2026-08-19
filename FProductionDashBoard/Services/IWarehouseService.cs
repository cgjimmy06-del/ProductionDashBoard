using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
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
