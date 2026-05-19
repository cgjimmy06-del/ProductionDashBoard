using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface ITimeSlotLookupRepository : IRepository<TimeSlotLookup, MesDbContext>
    {
        public Task<int?> GetCurrentTimeSlotIdAsync(DateTime businessDate);
        public int? GetCurrentTimeSlotId(DateTime businessDate, List<TimeSlotLookup> timeslots);
    }
}
