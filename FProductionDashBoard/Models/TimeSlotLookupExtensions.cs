using System;

namespace FProductionDashBoard.Models;

internal static class TimeSlotLookupExtensions
{
    // 跨日邏輯: 跨整點 EndAt + 1；跨日 StartAt, EndAt + 1
    internal static (DateTime slotStart, DateTime slotEnd) GetBounds(
        this TimeSlotLookup slot, DateTime businessDate)
    {
        var slotStart = businessDate.Date.Add(slot.StartAt);
        var slotEnd   = businessDate.Date.Add(slot.EndAt);
        if (slot.IsCrossDay)
        {
            slotEnd = slotEnd.AddDays(1);
            if (slot.EndAt > slot.StartAt)
                slotStart = slotStart.AddDays(1);
        }
        return (slotStart, slotEnd);
    }
}
