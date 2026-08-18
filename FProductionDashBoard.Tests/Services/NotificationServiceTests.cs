using FProductionDashBoard.Services;
using System.Collections.Generic;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class NotificationServiceTests
    {
        private sealed class RecordingChannel : INotificationChannel
        {
            public List<Notification> Published { get; } = new();
            public void Publish(Notification notification) => Published.Add(notification);
        }

        // 建立受測組合：真實 ConnectionStatusService + 記錄用通道。
        // NotificationService 於 ctor 訂閱 status，透過事件被 status 參照持有，無需回傳即保持存活。
        private static (ConnectionStatusService status, RecordingChannel channel) Build()
        {
            var status = new ConnectionStatusService();
            var channel = new RecordingChannel();
            _ = new NotificationService(status, new[] { channel });
            return (status, channel);
        }

        [Fact]
        public void FirstConnect_DoesNotPublish()
        {
            var (status, channel) = Build();

            status.Report(true);

            Assert.Empty(channel.Published);
        }

        [Fact]
        public void ReconnectWithoutPriorDisconnect_DoesNotPublish()
        {
            var (status, channel) = Build();

            // 起始即為連線、之後從未斷線 → 不應發任何通知
            status.Report(true);

            Assert.Empty(channel.Published);
        }

        [Fact]
        public void Disconnect_PublishesErrorPersistent()
        {
            var (status, channel) = Build();
            status.Report(true); // 起始連線（不發）

            status.Report(false);

            var n = Assert.Single(channel.Published);
            Assert.Equal(NotificationSeverity.Error, n.Severity);
            Assert.True(n.IsPersistent);
        }

        [Fact]
        public void Reconnect_AfterDisconnect_PublishesSuccessTransient()
        {
            var (status, channel) = Build();
            status.Report(true);  // 起始連線（不發）
            status.Report(false); // 斷線（Error 常駐）

            status.Report(true);  // 恢復（Success 暫態）

            Assert.Equal(2, channel.Published.Count);
            var recovered = channel.Published[1];
            Assert.Equal(NotificationSeverity.Success, recovered.Severity);
            Assert.False(recovered.IsPersistent);
            Assert.NotNull(recovered.AutoDismissSeconds);
        }

        [Fact]
        public void RepeatedSameState_DoesNotPublishAgain()
        {
            var (status, channel) = Build();

            status.Report(false); // 斷線（Error）
            status.Report(false); // 同狀態，邊緣觸發不再發
            status.Report(false);

            var n = Assert.Single(channel.Published);
            Assert.Equal(NotificationSeverity.Error, n.Severity);
        }
    }
}
