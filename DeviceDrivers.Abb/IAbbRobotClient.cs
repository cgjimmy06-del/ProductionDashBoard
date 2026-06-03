namespace DeviceDrivers.Abb;

/// <summary>
/// ABB 機器人控制器用戶端。Stage 1：控制器探索、連線/斷線、狀態快照、
/// Task/Module 查詢、RAPID 變數讀寫（bool / string / num）。
/// Stage 2：新增 <see cref="StatusChanged"/> 事件，連線後自動訂閱四種 SDK 狀態事件。
/// <para>
/// 所有失敗一律以 <see cref="AbbRobotException"/> 回報，不漏出 ABB SDK 型別；
/// 連線失敗不會讓進程崩潰，呼叫端可持續運行並隨時再次 <see cref="Connect"/> 重連。
/// </para>
/// <para>
/// <b>執行緒注意</b>：<see cref="Connect"/>、<see cref="Disconnect"/>、Read/Write 等方法
/// 限單一執行緒使用。<see cref="StatusChanged"/> 由 ABB SDK 內部執行緒觸發——
/// 消費者必須自行 dispatch 至 UI 執行緒，且勿在其 handler 內呼叫 Read/Write 方法。
/// </para>
/// </summary>
public interface IAbbRobotClient : IDisposable
{
    /// <summary>目前是否已連線至控制器。此屬性不會拋出例外，可隨時輪詢。</summary>
    bool IsConnected { get; }

    /// <summary>
    /// 在 ABB SDK 內部執行緒上觸發，當連線狀態、Controller State、Operating Mode 或
    /// Execution Status 任一發生變化時發射。消費者必須自行 dispatch 至 UI 執行緒。
    /// 訂閱由 <see cref="Connect"/> 後自動開始、<see cref="Disconnect"/>/<see cref="IDisposable.Dispose"/>
    /// 後自動取消。勿在 handler 內呼叫 ReadBool/ReadNum/ReadString/Write* 方法。
    /// </summary>
    event EventHandler<AbbRobotStatus>? StatusChanged;

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
    /// <remarks>
    /// <b>同步阻塞</b>：網路不通時底層連線會阻塞至逾時（可能數秒）。離線後的定期重連請在
    /// <b>背景執行緒</b>呼叫，勿在 UI 執行緒，以免畫面凍結。<br/>
    /// 失敗時內部狀態保持乾淨（<see cref="IsConnected"/> 為 <c>false</c>、不殘留半死連線），
    /// 呼叫端只要 catch <see cref="AbbRobotException"/> 即可稍後重試，單一設備連線失敗不影響進程運行。<br/>
    /// 配合類別層級「非執行緒安全」約束，勿與其他方法跨執行緒併用同一實例。
    /// </remarks>
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

    /// <summary>
    /// 列出指定 Task / Module 之下的 RAPID 資料變數（僅當前 module 層級，不遞迴）。
    /// 每筆含變數名稱、資料型別與種類（VAR / PERS / CONST）。
    /// </summary>
    /// <param name="taskName">Task 名稱。</param>
    /// <param name="moduleName">Module 名稱。</param>
    /// <exception cref="AbbRobotException">尚未連線（<see cref="AbbRobotErrorKind.NotConnected"/>）或讀取失敗。</exception>
    IReadOnlyList<AbbRapidSymbolInfo> GetModuleVariables(string taskName, string moduleName);

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

    /// <summary>讀取一個 RAPID 陣列變數，以 RAPID 字串格式（如 [1,2,3]）回傳。</summary>
    /// <exception cref="AbbRobotException">尚未連線、非陣列型別或讀取失敗。</exception>
    string ReadArray(RapidVariableAddress address);

    /// <summary>以 RAPID 字串格式（如 [1,2,3]）寫入一個陣列變數。</summary>
    /// <exception cref="AbbRobotException">尚未連線、非陣列型別或寫入失敗。</exception>
    void WriteArray(RapidVariableAddress address, string rapidString);
}
