using FProductionDashBoard.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class ConnectionStatusServiceTests
    {
        [Fact]
        public void Report_FirstCallTrue_RaisesEventAndIsConnected()
        {
            var svc = new ConnectionStatusService();
            int raised = 0;
            svc.StatusChanged += (_, _) => raised++;

            svc.Report(true);

            Assert.Equal(1, raised);
            Assert.True(svc.IsConnected);
            Assert.NotNull(svc.LastChangedAt);
        }

        [Fact]
        public void Report_FirstCallFalse_RaisesEvent()
        {
            var svc = new ConnectionStatusService();
            int raised = 0;
            svc.StatusChanged += (_, _) => raised++;

            svc.Report(false);

            Assert.Equal(1, raised);
            Assert.False(svc.IsConnected);
            Assert.NotNull(svc.LastChangedAt);
        }

        [Fact]
        public void Report_SameValueRepeated_DoesNotRaiseAgain()
        {
            var svc = new ConnectionStatusService();
            int raised = 0;
            svc.StatusChanged += (_, _) => raised++;

            svc.Report(true);
            svc.Report(true);
            svc.Report(true);

            Assert.Equal(1, raised);
        }

        [Fact]
        public void Report_ValueFlips_RaisesEventAndUpdatesLastChangedAt()
        {
            var svc = new ConnectionStatusService();
            svc.Report(true);
            var firstChangedAt = svc.LastChangedAt;
            int raised = 0;
            svc.StatusChanged += (_, _) => raised++;

            svc.Report(false);

            Assert.Equal(1, raised);
            Assert.False(svc.IsConnected);
            Assert.NotNull(svc.LastChangedAt);
            Assert.True(svc.LastChangedAt >= firstChangedAt);
        }

        [Fact]
        public void GetSnapshot_ReturnsPairMatchingCurrentState()
        {
            var svc = new ConnectionStatusService();

            svc.Report(true);
            var snapshot = svc.GetSnapshot();

            Assert.Equal(svc.IsConnected, snapshot.IsConnected);
            Assert.Equal(svc.LastChangedAt, snapshot.LastChangedAt);
            Assert.True(snapshot.IsConnected);
            Assert.NotNull(snapshot.LastChangedAt);
        }

        [Fact]
        public void Report_ConcurrentCalls_DoesNotThrowAndRaisesWithinBounds()
        {
            var svc = new ConnectionStatusService();
            int raised = 0;
            svc.StatusChanged += (_, _) => System.Threading.Interlocked.Increment(ref raised);

            Parallel.For(0, 200, i =>
            {
                svc.Report(i % 2 == 0);
            });

            // 每次 raise 對應一次狀態翻轉，次數必落在 1 與呼叫總數之間；最終狀態不可預期，不做斷言
            Assert.InRange(raised, 1, 200);
            Assert.NotNull(svc.LastChangedAt);
        }
    }
}
