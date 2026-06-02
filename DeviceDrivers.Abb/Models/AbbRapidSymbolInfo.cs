namespace DeviceDrivers.Abb;

/// <summary>
/// 單一 RAPID 資料變數的快照。由 <see cref="IAbbRobotClient.GetModuleVariables"/> 產生。
/// </summary>
public sealed record AbbRapidSymbolInfo
{
    /// <summary>變數名稱。</summary>
    public required string Name { get; init; }

    /// <summary>RAPID 資料型別（如 num / bool / string / robtarget）；取得失敗時為空字串。</summary>
    public required string DataType { get; init; }

    /// <summary>變數種類（VAR / PERS / CONST）；無法判定時為空字串。</summary>
    public required string Kind { get; init; }
}
