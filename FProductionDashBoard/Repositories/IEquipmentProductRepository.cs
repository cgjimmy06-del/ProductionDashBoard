using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IEquipmentProductRepository : IRepository<EquipmentProduct, MesDbContext>
    {
        Task<List<EquipmentProduct>> GetByEquipmentAsync(int equipmentId);
    }
}
