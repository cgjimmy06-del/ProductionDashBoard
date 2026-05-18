namespace DeviceDrivers.Abb;

/// <summary>
/// ABB 機器人操作失敗一律以本例外回報；包裝底層的 ABB SDK / COM / 網路例外，
/// 讓呼叫端永遠面對單一例外型別、不漏出廠商型別。
/// 透過 <see cref="Kind"/> 區分「該重連」（如 <see cref="AbbRobotErrorKind.ConnectionFailed"/>）
/// 與「真錯誤」。
/// </summary>
public sealed class AbbRobotException : Exception
{
    /// <summary>建立例外。</summary>
    /// <param name="kind">錯誤分類。</param>
    /// <param name="message">錯誤訊息，慣例上以 <c>[方法名]</c> 前綴。</param>
    /// <param name="innerException">被包裝的底層例外（若有）。</param>
    public AbbRobotException(AbbRobotErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>錯誤分類。</summary>
    public AbbRobotErrorKind Kind { get; }
}
