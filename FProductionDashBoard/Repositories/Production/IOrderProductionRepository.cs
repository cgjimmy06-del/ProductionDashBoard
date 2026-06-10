using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IOrderProductionRepository
    {
        Task<bool> CheckConnectionAsync();
        Task<List<OrderProduction>> GetAllAsync();
        // Returns Pending + InProduction + Completed orders (excludes Cancelled), ordered by create_at asc
        Task<List<OrderProduction>> GetByEquipmentAsync(int equipmentId);
        Task<List<OrderProduction>> GetByScheduleAsync(int scheduleId);
        Task<OrderProduction?> GetInProductionByEquipmentAsync(int equipmentId);
        Task<int> AddAsync(int equipmentId, int equipmentProductId, int? quantity, int createdBy, int? scheduleId = null);
        Task StartAsync(int orderId, int startedBy, DateTime startedAt);
        Task EndAsync(int orderId, DateTime endedAt);
        Task CancelAsync(int orderId, string? description);
    }
}
