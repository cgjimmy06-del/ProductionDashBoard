using System;
using FProductionDashBoard.Models;
using Xunit;

namespace FProductionDashBoard.Tests.Models
{
    /// <summary>
    /// TimeSlotLookupExtensions.GetBounds 的純函式邊界測試。
    /// GetBounds 靠 IsCrossDay 旗標 +（EndAt &gt; StartAt）決定日期位移，是巡檢時段跨日判斷的核心，
    /// 由 DataService 的顏色狀態 / 補漏檢 / 當前時段判斷共同消費；位移寫錯會靜默污染巡檢稽核資料。
    /// 全部用固定 businessDate 建構，結果完全確定、不依賴 DateTime.Now。
    /// </summary>
    public class TimeSlotLookupExtensionsTests
    {
        // 固定基準日，避免任何時鐘相依
        private static readonly DateTime BusinessDate = new(2026, 3, 10);

        [Fact]
        public void GetBounds_SameDaySlot_NoShift()
        {
            var slot = new TimeSlotLookup
            {
                StartAt    = new TimeSpan(9, 0, 0),
                EndAt      = new TimeSpan(17, 0, 0),
                IsCrossDay = false
            };

            var (slotStart, slotEnd) = slot.GetBounds(BusinessDate);

            Assert.Equal(new DateTime(2026, 3, 10, 9, 0, 0), slotStart);
            Assert.Equal(new DateTime(2026, 3, 10, 17, 0, 0), slotEnd);
        }

        [Fact]
        public void GetBounds_CrossMidnight_OnlyEndShifted()
        {
            // 跨半夜型（EndAt &lt; StartAt）：start 留在當天、end +1 天
            var slot = new TimeSlotLookup
            {
                StartAt    = new TimeSpan(23, 0, 0),
                EndAt      = new TimeSpan(1, 0, 0),
                IsCrossDay = true
            };

            var (slotStart, slotEnd) = slot.GetBounds(BusinessDate);

            Assert.Equal(new DateTime(2026, 3, 10, 23, 0, 0), slotStart);
            Assert.Equal(new DateTime(2026, 3, 11, 1, 0, 0), slotEnd);
        }

        [Fact]
        public void GetBounds_CrossDayBothShifted_BothPlusOneDay()
        {
            // 兩端位移型（IsCrossDay 且 EndAt &gt; StartAt）：整段搬到隔天凌晨（次一營業日凌晨班）
            var slot = new TimeSlotLookup
            {
                StartAt    = new TimeSpan(1, 0, 0),
                EndAt      = new TimeSpan(2, 0, 0),
                IsCrossDay = true
            };

            var (slotStart, slotEnd) = slot.GetBounds(BusinessDate);

            Assert.Equal(new DateTime(2026, 3, 11, 1, 0, 0), slotStart);
            Assert.Equal(new DateTime(2026, 3, 11, 2, 0, 0), slotEnd);
        }

        [Fact]
        public void GetBounds_CrossDayEqualStartEnd_LocksCurrentBehavior()
        {
            // 邊界：EndAt == StartAt 且 IsCrossDay。因（EndAt &gt; StartAt）為 false → start 不位移、end +1 天，
            // 形成 24 小時窗。此為現況行為（非「應然」），測試僅鎖住現況以防未來無意間改動。
            var slot = new TimeSlotLookup
            {
                StartAt    = new TimeSpan(2, 0, 0),
                EndAt      = new TimeSpan(2, 0, 0),
                IsCrossDay = true
            };

            var (slotStart, slotEnd) = slot.GetBounds(BusinessDate);

            Assert.Equal(new DateTime(2026, 3, 10, 2, 0, 0), slotStart);
            Assert.Equal(new DateTime(2026, 3, 11, 2, 0, 0), slotEnd);
        }

        [Fact]
        public void GetBounds_BusinessDateWithTime_TruncatedToDate()
        {
            // businessDate 帶時間部分時，應以 .Date 為基準（截斷時間）再加 slot 的 TimeSpan
            var businessDateWithTime = new DateTime(2026, 3, 10, 15, 30, 45);
            var slot = new TimeSlotLookup
            {
                StartAt    = new TimeSpan(9, 0, 0),
                EndAt      = new TimeSpan(17, 0, 0),
                IsCrossDay = false
            };

            var (slotStart, slotEnd) = slot.GetBounds(businessDateWithTime);

            Assert.Equal(new DateTime(2026, 3, 10, 9, 0, 0), slotStart);
            Assert.Equal(new DateTime(2026, 3, 10, 17, 0, 0), slotEnd);
        }
    }
}
