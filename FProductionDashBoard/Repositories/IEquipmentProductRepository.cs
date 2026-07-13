using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IEquipmentProductRepository : IRepository<EquipmentProduct, MesDbContext>
    {
        Task<List<EquipmentProduct>> GetByEquipmentAsync(int equipmentId);
        Task<DateTime> BulkUpdateProductionStatusAsync(IEnumerable<int> equipmentProductIds, TuningType newStatus);
    }
}
