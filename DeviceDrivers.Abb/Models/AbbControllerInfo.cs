namespace DeviceDrivers.Abb;

/// <summary>
/// 控制器探索結果的不可變快照。由 <see cref="IAbbRobotClient.DiscoverControllers"/> 產生。
/// </summary>
public sealed record AbbControllerInfo
{
    /// <summary>控制器名稱。</summary>
    public required string ControllerName { get; init; }

    /// <summary>控制器 IP 位址。</summary>
    public required string IpAddress { get; init; }

    /// <summary>控制器上的 RobotWare 系統名稱。</summary>
    public required string SystemName { get; init; }

    /// <summary>探索當下控制器是否可供連線。</summary>
    public required bool IsAvailable { get; init; }
}
