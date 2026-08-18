namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 通知送達通道抽象。Snackbar 浮層為首個實作；未來 Email／IM 沿用同契約接入，非另一個 service。
    /// </summary>
    public interface INotificationChannel
    {
        /// <summary>
        /// 送出一則通知。實作可能於背景執行緒被呼叫，需自行負責必要的執行緒切換（如 UI 通道 marshal 至 UI 執行緒）。
        /// </summary>
        void Publish(Notification notification);
    }
}
