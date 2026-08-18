using System;

namespace FProductionDashBoard.Services
{
    /// <summary>
    /// 通知嚴重度。決定浮層配色與（未來）路由/節流分級。
    /// </summary>
    public enum NotificationSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// 統一通知模型：各訊號源事件經 NotificationService 轉為此型別後分派至各 INotificationChannel。
    /// </summary>
    /// <param name="Severity">嚴重度（決定配色）。</param>
    /// <param name="Message">已在地化的顯示文字（建立當下擷取）。</param>
    /// <param name="Timestamp">事件發生時間。</param>
    /// <param name="IsPersistent">是否常駐至下一則覆蓋（斷線類）；false 表暫態，依 <paramref name="AutoDismissSeconds"/> 自動收起。</param>
    /// <param name="AutoDismissSeconds">暫態通知的自動收起秒數；IsPersistent 為 true 時忽略。</param>
    /// <param name="Key">選填去重鍵，供未來同鍵合併/節流使用。</param>
    public record Notification(
        NotificationSeverity Severity,
        string Message,
        DateTime Timestamp,
        bool IsPersistent,
        int? AutoDismissSeconds = null,
        string? Key = null);
}
