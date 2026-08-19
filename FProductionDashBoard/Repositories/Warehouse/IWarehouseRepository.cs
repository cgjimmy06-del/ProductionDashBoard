using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IWarehouseRepository : IRepository<StorageLocation, MesDbContext>
    {
        Task<bool> ExistsLocationCodeAsync(string code, int? excludeLocationId = null);
        Task<bool> HasActiveAssignmentAsync(int scheduleId);
        Task<bool> HasActiveAssignmentsAtLocationAsync(int locationId);
        Task<int> AssignAsync(int locationId, int scheduleId, int assignedBy);
        Task ReleaseAsync(int scheduleId, int releasedBy);
        Task<List<LocationAssignment>> GetActiveAssignmentsAsync(int locationId);
        Task<Dictionary<int, int>> GetActiveCountByLocationAsync();
    }
}
