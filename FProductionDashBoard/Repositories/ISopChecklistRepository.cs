using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface ISopChecklistRepository : IRepository<SopChecklist, MesDbContext>
    {
        Task<SopChecklist?> GetWithItemsAsync(int sopId);
        Task<List<WorkProcess>> GetProcessesAsync();
        Task<int> AddProductModelAsync(string name, string? remark);
    }
}
