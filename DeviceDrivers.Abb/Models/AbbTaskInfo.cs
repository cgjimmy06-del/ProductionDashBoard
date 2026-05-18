namespace DeviceDrivers.Abb;

/// <summary>
/// RAPID Task 的快照，含其下的模組名稱清單。由 <see cref="IAbbRobotClient.GetTasks"/> 產生。
/// </summary>
public sealed record AbbTaskInfo
{
    /// <summary>Task 名稱。</summary>
    public required string Name { get; init; }

    /// <summary>Task 的執行狀態。</summary>
    public required AbbExecutionStatus ExecutionStatus { get; init; }

    /// <summary>Task 之下的模組名稱清單。</summary>
    public required IReadOnlyList<string> Modules { get; init; }
}
