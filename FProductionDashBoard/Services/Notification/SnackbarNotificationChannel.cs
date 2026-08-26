using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;
using System.Windows.Threading;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 通知通道：以 MaterialDesign Snackbar 浮層呈現，持有可綁定狀態供 MainWindow 綁定（IsActive / CurrentMessage / CurrentSeverity）。
    /// 常駐通知（斷線）維持 <see cref="IsActive"/>=true 至下一則覆蓋；暫態通知（恢復）以 DispatcherTimer 於指定秒數後自動收起。
    /// 訊號可能來自背景執行緒，<see cref="Publish"/> 一律 marshal 至 UI 執行緒後才更新狀態與操作 timer。
    /// 為 singleton，於 DI 建置（OnStartup UI 執行緒）時建構，故 DispatcherTimer 綁定 UI Dispatcher。
    /// </summary>
    public partial class SnackbarNotificationChannel : ObservableObject, INotificationChannel
    {
        private readonly DispatcherTimer _autoDismissTimer;

        [ObservableProperty] private bool isActive;
        [ObservableProperty] private string currentMessage = "";
        [ObservableProperty] private NotificationSeverity currentSeverity = NotificationSeverity.Info;

        public SnackbarNotificationChannel()
        {
            // 顯式綁定 UI Dispatcher，移除對「建構於 UI 執行緒」的隱含依賴
            _autoDismissTimer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher);
            _autoDismissTimer.Tick += OnAutoDismissTick;
        }

        public void Publish(Notification notification)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.BeginInvoke(() => Show(notification));
        }

        private void Show(Notification notification)
        {
            _autoDismissTimer.Stop();

            CurrentSeverity = notification.Severity;
            CurrentMessage = notification.Message;
            IsActive = true;

            if (!notification.IsPersistent)
            {
                _autoDismissTimer.Interval = TimeSpan.FromSeconds(notification.AutoDismissSeconds ?? 4);
                _autoDismissTimer.Start();
            }
        }

        private void OnAutoDismissTick(object? sender, EventArgs e)
        {
            _autoDismissTimer.Stop();
            IsActive = false;
        }
    }
}
