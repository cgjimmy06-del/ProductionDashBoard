namespace DeviceDrivers.Abb;

/// <summary>
/// ABB 控制器運轉狀態。對映 ABB SDK 的 <c>ControllerState</c>，讓對外型別與廠商型別隔離。
/// </summary>
public enum AbbControllerState
{
    /// <summary>無法判定（含對映失敗）。</summary>
    Unknown = 0,
    Init,
    MotorsOff,
    MotorsOn,
    GuardStop,
    EmergencyStop,
    EmergencyStopReset,
    SystemFailure,
}

/// <summary>
/// ABB 控制器操作模式。對映 ABB SDK 的 <c>ControllerOperatingMode</c>。
/// </summary>
public enum AbbOperatingMode
{
    /// <summary>無法判定（含對映失敗）。</summary>
    Unknown = 0,
    Init,
    Auto,
    ManualReducedSpeed,
    ManualFullSpeed,
    Undefined,
}

/// <summary>
/// RAPID 執行狀態。涵蓋控制器層 <c>ExecutionStatus</c> 與 Task 層 <c>TaskExecutionStatus</c> 的值。
/// </summary>
public enum AbbExecutionStatus
{
    /// <summary>無法判定（含對映失敗）。</summary>
    Unknown = 0,
    Running,
    Stopped,
    Ready,
    Started,
    Uninitialized,
}

/// <summary>
/// <see cref="AbbRobotException"/> 的錯誤分類，供呼叫端分辨「該重連」與「真錯誤」。
/// </summary>
public enum AbbRobotErrorKind
{
    /// <summary>未分類錯誤。</summary>
    Unknown = 0,
    /// <summary>連線失敗 —— 呼叫端可稍後再次 <c>Connect</c> 重試。</summary>
    ConnectionFailed,
    /// <summary>尚未連線就呼叫了需要連線的操作。</summary>
    NotConnected,
    /// <summary>操作逾時。</summary>
    Timeout,
    /// <summary>取得寫入主控權（Mastership）失敗。</summary>
    MastershipDenied,
    /// <summary>已連線但操作本身失敗。</summary>
    OperationFailed,
}
