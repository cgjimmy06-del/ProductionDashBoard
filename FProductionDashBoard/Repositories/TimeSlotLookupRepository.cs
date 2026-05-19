using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class TimeSlotLookupRepository : Repository<TimeSlotLookup, MesDbContext>, ITimeSlotLookupRepository
    {
        public TimeSlotLookupRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<int?> GetCurrentTimeSlotIdAsync(DateTime businessDate)
        {
            await using var ctx = _factory.CreateDbContext();
            var slots = await ctx.TimeSlotLookups.ToListAsync().ConfigureAwait(false);

            var now = DateTime.Now;
            foreach (var slot in slots)
            {
                var (slotStart, slotEnd) = slot.GetBounds(businessDate);
                if (now >= slotStart && now < slotEnd)
                    return slot.TimeSlotId;
            }
            return null;
        }

        public int? GetCurrentTimeSlotId(DateTime businessDate, List<TimeSlotLookup> timeslots)
        {
            var now = DateTime.Now;
            foreach (var slot in timeslots)
            {
                var (slotStart, slotEnd) = slot.GetBounds(businessDate);
                if (now >= slotStart && now < slotEnd)
                    return slot.TimeSlotId;
            }
            return null;
        }
    }
}
