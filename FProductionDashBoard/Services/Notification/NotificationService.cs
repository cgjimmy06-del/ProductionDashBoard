using System;
using System.Collections.Generic;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 通知彙整層：訂閱各訊號源（本切片為主資料庫連線狀態），將背景/系統事件轉為統一 <see cref="Notification"/> 後分派至各通道。
    /// 訊號源＝<see cref="ConnectionStatusService.StatusChanged"/>（邊緣觸發，僅狀態翻轉時發）；通道＝注入的 INotificationChannel 清單（首個為 Snackbar 浮層）。
    /// 為 singleton，需在啟動時主動解析一次以完成訂閱（見 App.xaml.cs）。
    /// </summary>
    public class NotificationService : IDisposable
    {
        private readonly ConnectionStatusService _connectionStatus;
        private readonly IReadOnlyList<INotificationChannel> _channels;
        private readonly EventHandler _onStatusChanged;
        private readonly object _gate = new();

        // 起始狀態抑制：僅在「曾顯示過斷線」後才對恢復發送成功通知，
        // 避免啟動時首次連線（或無前置斷線的連線）誤發綠色提示。
        // StatusChanged 於快速翻轉時可能由不同背景執行緒併發觸發（來源在 lock 外 invoke），
        // 故以 _gate 保護「讀快照→決策→改旗標」為原子操作。
        private bool _hasShownDisconnect;

        public NotificationService(
            ConnectionStatusService connectionStatus,
            IEnumerable<INotificationChannel> channels)
        {
            _connectionStatus = connectionStatus;
            _channels = new List<INotificationChannel>(channels);

            _onStatusChanged = (_, _) => OnConnectionStatusChanged();
            _connectionStatus.StatusChanged += _onStatusChanged;
        }

        private void OnConnectionStatusChanged()
        {
            lock (_gate)
            {
                Notification? notification = null;
                var snapshot = _connectionStatus.GetSnapshot();
                var since = snapshot.LastChangedAt?.ToString("yyyy/MM/dd HH:mm") ?? "-";

                if (!snapshot.IsConnected)
                {
                    _hasShownDisconnect = true;
                    notification = new Notification(
                        NotificationSeverity.Error,
                        string.Format(Properties.Resources.NotifyDbDisconnected, since),
                        snapshot.LastChangedAt ?? DateTime.Now,
                        IsPersistent: true);
                }
                else if (_hasShownDisconnect)
                {
                    _hasShownDisconnect = false;
                    notification = new Notification(
                        NotificationSeverity.Success,
                        string.Format(Properties.Resources.NotifyDbReconnected, since),
                        snapshot.LastChangedAt ?? DateTime.Now,
                        IsPersistent: false,
                        AutoDismissSeconds: 4);
                }

                // Dispatch 於鎖內：確保「決策順序＝送達順序」，避免斷線/恢復快速翻轉時
                // 兩背景執行緒的 BeginInvoke 入列倒置導致 UI 殘留假斷線狀態。
                // 通道 Publish 僅非阻塞 BeginInvoke，持鎖成本可忽略、無 reentrancy。
                if (notification != null)
                    Dispatch(notification);
            }
        }

        private void Dispatch(Notification notification)
        {
            foreach (var channel in _channels)
                channel.Publish(notification);
        }

        public void Dispose()
        {
            _connectionStatus.StatusChanged -= _onStatusChanged;
        }
    }
}
