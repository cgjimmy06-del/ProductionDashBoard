namespace DeviceDrivers.Abb;

/// <summary>
/// 機器人控制器的狀態快照。由 <see cref="IAbbRobotClient.GetStatus"/> 產生 —— 僅為當下快照，
/// 不做事件訂閱（Stage 1 範圍）。
/// </summary>
public sealed record AbbRobotStatus
{
    /// <summary>取得快照當下是否仍連線。</summary>
    public required bool IsConnected { get; init; }

    /// <summary>控制器名稱。</summary>
    public required string ControllerName { get; init; }

    /// <summary>控制器上的 RobotWare 系統名稱。</summary>
    public required string SystemName { get; init; }

    /// <summary>控制器運轉狀態。</summary>
    public required AbbControllerState State { get; init; }

    /// <summary>控制器操作模式。</summary>
    public required AbbOperatingMode OperatingMode { get; init; }

    /// <summary>RAPID 程式的執行狀態。</summary>
    public required AbbExecutionStatus RapidExecutionStatus { get; init; }
}
