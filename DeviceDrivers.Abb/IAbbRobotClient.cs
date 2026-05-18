namespace DeviceDrivers.Abb;

/// <summary>
/// ABB 機器人控制器用戶端。Stage 1 範圍：控制器探索、連線/斷線、狀態快照、
/// Task/Module 查詢、RAPID 變數讀寫（bool / string / num）。
/// <para>
/// 所有失敗一律以 <see cref="AbbRobotException"/> 回報，不漏出 ABB SDK 型別；
/// 連線失敗不會讓進程崩潰，呼叫端可持續運行並隨時再次 <see cref="Connect"/> 重連。
/// </para>
/// <para>
/// <b>非執行緒安全</b>：限單一執行緒使用，呼叫端自行確保。完整執行緒模型留待
/// Stage 2 加入事件訂閱時再設計。
/// </para>
/// </summary>
public interface IAbbRobotClient : IDisposable
{
    /// <summary>目前是否已連線至控制器。此屬性不會拋出例外，可隨時輪詢。</summary>
    bool IsConnected { get; }

    /// <summary>
    /// 探索網路上的 ABB 控制器。同網段的控制器會被自動掃描到；
    /// 不同網段的控制器，請將其 IP 透過 <paramref name="remoteIpHints"/> 傳入。
    /// </summary>
    /// <param name="remoteIpHints">不同網段控制器的 IP 提示（通常來自 DB 設備清單）。</param>
    /// <exception cref="AbbRobotException">探索過程發生錯誤。</exception>
    IReadOnlyList<AbbControllerInfo> DiscoverControllers(params string[] remoteIpHints);

    /// <summary>
    /// 連線至指定控制器。若先前未經 <see cref="DiscoverControllers"/> 探索到，
    /// 會以該 IP 為提示重新探索一次。
    /// </summary>
    /// <exception cref="AbbRobotException">連線失敗，<see cref="AbbRobotException.Kind"/> 為
    /// <see cref="AbbRobotErrorKind.ConnectionFailed"/>；呼叫端可稍後再次呼叫本方法重連。</exception>
    void Connect(AbbControllerInfo controller);

    /// <summary>中斷與控制器的連線。對「本來就未連線」的情況容錯，不拋例外。</summary>
    void Disconnect();

    /// <summary>取得控制器目前的狀態快照。</summary>
    /// <exception cref="AbbRobotException">尚未連線（<see cref="AbbRobotErrorKind.NotConnected"/>）或讀取失敗。</exception>
    AbbRobotStatus GetStatus();

    /// <summary>列出控制器上所有 RAPID Task 與其下的模組。</summary>
    /// <exception cref="AbbRobotException">尚未連線（<see cref="AbbRobotErrorKind.NotConnected"/>）或讀取失敗。</exception>
    IReadOnlyList<AbbTaskInfo> GetTasks();

    /// <summary>讀取一個 RAPID <c>bool</c> 變數。</summary>
    /// <exception cref="AbbRobotException">尚未連線或讀取失敗。</exception>
    bool ReadBool(RapidVariableAddress address);

    /// <summary>寫入一個 RAPID <c>bool</c> 變數。</summary>
    /// <exception cref="AbbRobotException">尚未連線或寫入失敗。</exception>
    void WriteBool(RapidVariableAddress address, bool value);

    /// <summary>讀取一個 RAPID <c>string</c> 變數。</summary>
    /// <exception cref="AbbRobotException">尚未連線或讀取失敗。</exception>
    string ReadString(RapidVariableAddress address);

    /// <summary>寫入一個 RAPID <c>string</c> 變數。</summary>
    /// <exception cref="AbbRobotException">尚未連線或寫入失敗。</exception>
    void WriteString(RapidVariableAddress address, string value);

    /// <summary>讀取一個 RAPID <c>num</c> 變數。</summary>
    /// <exception cref="AbbRobotException">尚未連線或讀取失敗。</exception>
    double ReadNum(RapidVariableAddress address);

    /// <summary>寫入一個 RAPID <c>num</c> 變數。</summary>
    /// <exception cref="AbbRobotException">尚未連線或寫入失敗。</exception>
    void WriteNum(RapidVariableAddress address, double value);
}
