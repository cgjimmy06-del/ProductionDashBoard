using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IScheduleRepository : IRepository<Schedule, MesDbContext>
    {
        Task<List<Schedule>> GetAllWithDetailsAsync();
        Task<Schedule?> GetByIdWithDetailsAsync(int scheduleId);
        Task<List<Schedule>> GetByStatusAsync(ScheduleStatus status);
        Task<List<Schedule>> GetChildrenAsync(int parentId);
        new Task<int> AddAsync(Schedule entity);
        Task MarkScheduledAsync(int scheduleId, int scheduledBy, DateTime scheduledAt);
        Task MarkVerifiedAsync(int scheduleId, int verifiedBy, DateTime verifiedAt, int? actualQuantity, string? description);
        Task MarkReleasedAsync(int scheduleId, int releasedBy, DateTime releasedAt, string? description);
        Task MarkCancelledAsync(int scheduleId, string? description);
        Task ForceCompleteAsync(int scheduleId, int verifiedBy, DateTime verifiedAt, int? actualQuantity, string description);
        Task<int> SplitScheduleAsync(int originalId, int remainingQuantity, int releasedBy, DateTime releasedAt, string? description);
        Task RecalcActualQuantityAsync(int scheduleId);
        Task MarkScheduledAndRecalcAsync(int scheduleId, int userId, DateTime now);
    }
}
