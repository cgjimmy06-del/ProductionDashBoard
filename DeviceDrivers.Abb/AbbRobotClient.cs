using System.Net;
using ABB.Robotics.Controllers;
using ABB.Robotics.Controllers.Discovery;
using ABB.Robotics.Controllers.RapidDomain;
using RapidString = ABB.Robotics.Controllers.RapidDomain.String;

namespace DeviceDrivers.Abb;

/// <summary>
/// <see cref="IAbbRobotClient"/> 的實作，包裝 ABB PC SDK 的 <see cref="Controller"/>。
/// <para>
/// 離線韌性：每個公開方法都會把底層 ABB SDK / COM / 網路例外包成 <see cref="AbbRobotException"/>，
/// 永不漏出原生例外、永不讓進程崩潰。連線中斷後呼叫端可再次 <see cref="Connect"/> 重連。
/// </para>
/// <para>
/// 記憶體管理：<see cref="Controller"/> / <see cref="RapidData"/> / <see cref="Mastership"/>
/// 皆為包裝 COM 資源的 <see cref="IDisposable"/>，本類別一律以 <c>using</c> 釋放；重連前會先
/// 釋放舊的 <see cref="Controller"/>，反覆連線不會洩漏。
/// </para>
/// <para>
/// <b>執行緒安全</b>：Connect / Disconnect / Dispose / GetStatus / GetTasks /
/// GetModuleVariables / Read / Write 等操作方法皆以內部 <c>lock</c> 序列化，可安全地由不同
/// 執行緒呼叫（同一時間只會有一個操作觸及底層 COM <see cref="Controller"/>）。
/// <see cref="IAbbRobotClient.StatusChanged"/> 由 ABB SDK 內部執行緒觸發，此為設計預期行為；
/// 其發射（FireStatus）刻意不進入鎖（避免阻塞 SDK callback 執行緒），只讀狀態快照、為 best-effort；
/// 消費者必須自行 dispatch 至 UI 執行緒，且勿在其 handler 內呼叫 Read/Write 方法。
/// </para>
/// </summary>
public sealed class AbbRobotClient : IAbbRobotClient
{
    private Controller? _controller;
    private IReadOnlyList<ControllerInfo> _lastScan = Array.Empty<ControllerInfo>();
    private bool _disposed;

    /// <summary>序列化所有觸及底層 COM <see cref="Controller"/> 的操作；FireStatus 刻意不取用。</summary>
    private readonly object _gate = new();

    private EventHandler<ConnectionChangedEventArgs>?      _onConnectionChanged;
    private EventHandler<OperatingModeChangeEventArgs>?    _onOperatingModeChanged;
    private EventHandler<StateChangedEventArgs>?           _onStateChanged;
    private EventHandler<ExecutionStatusChangedEventArgs>? _onExecutionStatusChanged;

    /// <inheritdoc/>
    public event EventHandler<AbbRobotStatus>? StatusChanged;

    /// <inheritdoc/>
    public bool IsConnected => !_disposed && _controller is { Connected: true };

    /// <inheritdoc/>
    public IReadOnlyList<AbbControllerInfo> DiscoverControllers(params string[] remoteIpHints)
    {
        ThrowIfDisposed();
        try
        {
            _lastScan = Scan(remoteIpHints);
            return _lastScan.Select(ToAbbControllerInfo).ToArray();
        }
        catch (Exception ex)
        {
            throw Wrap(nameof(DiscoverControllers), AbbRobotErrorKind.OperationFailed, ex);
        }
    }

    /// <inheritdoc/>
    public void Connect(AbbControllerInfo controller)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(controller);
        lock (_gate)
        {
            try
            {
                ControllerInfo native = ResolveNative(controller.IpAddress);
                DisposeController();   // 重連前先釋放舊的 Controller，避免洩漏
                Controller connected = Controller.Connect(native, ConnectionType.Standalone);
                try
                {
                    connected.Logon(UserInfo.DefaultUser);
                }
                catch
                {
                    // Logon 失敗時 connected 尚未指派給 _controller，DisposeController 無法觸及，
                    // 必須在此就地釋放，否則反覆重連會洩漏已開連線 session 的 Controller。
                    // 釋放本身的例外不可掩蓋原始 Logon 失敗例外，故吞掉後以 throw; 重拋原例外。
                    try { connected.Dispose(); } catch { /* 釋放失敗不影響重拋原例外 */ }
                    throw;
                }
                _controller = connected;
                StartMonitoring();
            }
            catch (AbbRobotException)
            {
                throw;   // ResolveNative 丟出的已分類例外，原樣傳遞
            }
            catch (Exception ex)
            {
                throw Wrap(nameof(Connect), AbbRobotErrorKind.ConnectionFailed, ex);
            }
        }
    }

    /// <inheritdoc/>
    public void Disconnect()
    {
        if (_disposed) return;
        lock (_gate)
        {
            try
            {
                StopMonitoring();
                DisposeController();
            }
            catch { /* 斷線容錯：本來就未連線也不拋例外 */ }
        }
    }

    /// <inheritdoc/>
    public AbbRobotStatus GetStatus()
    {
        lock (_gate)
        {
            Controller c = RequireConnected(nameof(GetStatus));
            try
            {
                return BuildStatus(c);
            }
            catch (Exception ex)
            {
                throw Wrap(nameof(GetStatus), AbbRobotErrorKind.OperationFailed, ex);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<AbbTaskInfo> GetTasks()
    {
        lock (_gate)
        {
            Controller c = RequireConnected(nameof(GetTasks));
            try
            {
                var result = new List<AbbTaskInfo>();
                foreach (var task in c.Rapid.GetTasks())
                {
                    var modules = new List<string>();
                    try
                    {
                        foreach (var module in task.GetModules())
                            modules.Add(module.Name);
                    }
                    catch (Exception)
                    {
                        // 個別 task 無法列出模組時略過該 task 的模組清單（留空），不影響其他 task。
                        // 接住 ex 而非無參數 catch，保留可診斷的例外型別資訊。
                    }

                    result.Add(new AbbTaskInfo
                    {
                        Name = task.Name,
                        ExecutionStatus = ParseEnum<AbbExecutionStatus>(task.ExecutionStatus.ToString()),
                        Modules = modules,
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                throw Wrap(nameof(GetTasks), AbbRobotErrorKind.OperationFailed, ex);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<AbbRapidSymbolInfo> GetModuleVariables(string taskName, string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        lock (_gate)
        {
            Controller c = RequireConnected(nameof(GetModuleVariables));
            try
            {
                var task = c.Rapid.GetTask(taskName)
                    ?? throw new AbbRobotException(AbbRobotErrorKind.OperationFailed,
                           $"[GetModuleVariables] 找不到 Task {taskName}。");
                var module = task.GetModule(moduleName)
                    ?? throw new AbbRobotException(AbbRobotErrorKind.OperationFailed,
                           $"[GetModuleVariables] 找不到 Module {moduleName}。");

                // 只搜當前 module 層級的資料變數（recursive: false），不含 routine。
                var props = RapidSymbolSearchProperties.CreateDefaultForData(recursive: false);
                var result = new List<AbbRapidSymbolInfo>();
                foreach (RapidSymbol symbol in module.SearchRapidSymbol(props))
                {
                    string dataType = string.Empty;
                    bool isArray = false;
                    try
                    {
                        using RapidData rd = module.GetRapidData(symbol);
                        dataType = rd.RapidType ?? string.Empty;
                        isArray  = rd.Value is ArrayData;
                    }
                    catch (Exception)
                    {
                        // 個別變數型別讀取失敗時留空，不影響其他變數列舉。
                        // 接住 ex 而非無參數 catch，保留可診斷的例外型別資訊。
                    }

                    result.Add(new AbbRapidSymbolInfo
                    {
                        Name     = symbol.Name,
                        DataType = dataType,
                        Kind     = ToSymbolKind(symbol.Type),
                        IsArray  = isArray,
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                throw Wrap(nameof(GetModuleVariables), AbbRobotErrorKind.OperationFailed, ex);
            }
        }
    }

    /// <inheritdoc/>
    public bool ReadBool(RapidVariableAddress address)
        => Read(address, nameof(ReadBool), rd => ((Bool)rd.Value).Value);

    /// <inheritdoc/>
    public string ReadString(RapidVariableAddress address)
        => Read(address, nameof(ReadString), rd => ((RapidString)rd.Value).Value);

    /// <inheritdoc/>
    public double ReadNum(RapidVariableAddress address)
        => Read(address, nameof(ReadNum), rd => (double)((Num)rd.Value).Value);

    /// <inheritdoc/>
    public void WriteBool(RapidVariableAddress address, bool value)
        => Write(address, nameof(WriteBool), new Bool(value));

    /// <inheritdoc/>
    public void WriteString(RapidVariableAddress address, string value)
        => Write(address, nameof(WriteString), new RapidString(value ?? string.Empty));

    /// <inheritdoc/>
    public void WriteNum(RapidVariableAddress address, double value)
        => Write(address, nameof(WriteNum), new Num((float)value));

    /// <inheritdoc/>
    public string ReadArray(RapidVariableAddress address)
        => Read(address, nameof(ReadArray), rd =>
        {
            if (rd.Value is not ArrayData)
                throw new AbbRobotException(AbbRobotErrorKind.OperationFailed,
                    $"[ReadArray] {address.Variable} 不是陣列型別。");
            return rd.StringValue;
        });

    /// <inheritdoc/>
    public void WriteArray(RapidVariableAddress address, string rapidString)
    {
        ArgumentNullException.ThrowIfNull(address);
        lock (_gate)
        {
            Controller c = RequireConnected(nameof(WriteArray));
            try
            {
                using Mastership mastership = Mastership.Request(c);
                using RapidData rd = c.Rapid.GetRapidData(address.Task, address.Module, address.Variable);
                if (rd.Value is not ArrayData)
                    throw new AbbRobotException(AbbRobotErrorKind.OperationFailed,
                        $"[WriteArray] {address.Variable} 不是陣列型別。");
                rd.StringValue = rapidString;
            }
            catch (Exception ex)
            {
                throw Wrap($"{nameof(WriteArray)}({address})", AbbRobotErrorKind.OperationFailed, ex);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                StopMonitoring();
                DisposeController();
            }
            catch { /* Dispose 不拋例外 */ }
        }
    }

    // --- 內部輔助 -----------------------------------------------------------------

    private T Read<T>(RapidVariableAddress address, string method, Func<RapidData, T> extract)
    {
        ArgumentNullException.ThrowIfNull(address);
        lock (_gate)
        {
            Controller c = RequireConnected(method);
            try
            {
                using RapidData rd = c.Rapid.GetRapidData(address.Task, address.Module, address.Variable);
                return extract(rd);
            }
            catch (Exception ex)
            {
                throw Wrap($"{method}({address})", AbbRobotErrorKind.OperationFailed, ex);
            }
        }
    }

    private void Write(RapidVariableAddress address, string method, IRapidData value)
    {
        ArgumentNullException.ThrowIfNull(address);
        lock (_gate)
        {
            Controller c = RequireConnected(method);
            try
            {
                using Mastership mastership = Mastership.Request(c);
                using RapidData rd = c.Rapid.GetRapidData(address.Task, address.Module, address.Variable);
                rd.Value = value;
            }
            catch (Exception ex)
            {
                throw Wrap($"{method}({address})", AbbRobotErrorKind.OperationFailed, ex);
            }
        }
    }

    /// <summary>掃描網路控制器，<paramref name="remoteIpHints"/> 內合法的 IP 會先加為遠端提示。</summary>
    private static IReadOnlyList<ControllerInfo> Scan(IEnumerable<string>? remoteIpHints)
    {
        foreach (string hint in remoteIpHints ?? Array.Empty<string>())
        {
            if (IPAddress.TryParse(hint, out IPAddress? ip))
                NetworkScanner.AddRemoteController(ip);
        }

        var scanner = new NetworkScanner();
        scanner.Scan();
        return scanner.Controllers.Cast<ControllerInfo>().ToArray();
    }

    /// <summary>由 IP 找出對應的原生 <see cref="ControllerInfo"/>；快取沒有就以該 IP 重掃一次。</summary>
    private ControllerInfo ResolveNative(string ipAddress)
    {
        ControllerInfo? match = FindByIp(_lastScan, ipAddress);
        if (match is not null) return match;

        _lastScan = Scan(new[] { ipAddress });
        match = FindByIp(_lastScan, ipAddress);
        if (match is not null) return match;

        throw new AbbRobotException(
            AbbRobotErrorKind.ConnectionFailed,
            $"[Connect] 找不到 IP 為 {ipAddress} 的控制器。");
    }

    private static ControllerInfo? FindByIp(IEnumerable<ControllerInfo> infos, string ipAddress)
        => infos.FirstOrDefault(c => string.Equals(
               c.IPAddress?.ToString(), ipAddress, StringComparison.OrdinalIgnoreCase));

    private Controller RequireConnected(string method)
    {
        ThrowIfDisposed();
        if (_controller is not { Connected: true })
        {
            throw new AbbRobotException(
                AbbRobotErrorKind.NotConnected, $"[{method}] 尚未連線至控制器。");
        }
        return _controller;
    }

    private static AbbControllerInfo ToAbbControllerInfo(ControllerInfo info) => new()
    {
        ControllerName = info.ControllerName ?? string.Empty,
        IpAddress = info.IPAddress?.ToString() ?? string.Empty,
        SystemName = info.SystemName ?? string.Empty,
        IsAvailable = info.Availability == Availability.Available,
    };

    /// <summary>把 ABB SDK enum 的字串表示對映成本專案 enum；對映不到時回 <c>Unknown</c>（= 0）。</summary>
    private static TEnum ParseEnum<TEnum>(string raw) where TEnum : struct, Enum
        => Enum.TryParse(raw, ignoreCase: true, out TEnum value) ? value : default;

    /// <summary>把 RAPID 符號類型（flags）對映為簡短的變數種類字串；非資料變數回空字串。</summary>
    private static string ToSymbolKind(SymbolTypes type) =>
        type.HasFlag(SymbolTypes.Constant)   ? "CONST" :
        type.HasFlag(SymbolTypes.Persistent) ? "PERS"  :
        type.HasFlag(SymbolTypes.Variable)   ? "VAR"   :
        string.Empty;

    /// <summary>由 <see cref="Controller"/> 組出狀態快照。<see cref="GetStatus"/> 與 <see cref="FireStatus"/> 共用。</summary>
    private static AbbRobotStatus BuildStatus(Controller c) => new()
    {
        IsConnected          = c.Connected,
        ControllerName       = c.Name ?? string.Empty,
        SystemName           = c.SystemName ?? string.Empty,
        State                = ParseEnum<AbbControllerState>(c.State.ToString()),
        OperatingMode        = ParseEnum<AbbOperatingMode>(c.OperatingMode.ToString()),
        RapidExecutionStatus = ParseEnum<AbbExecutionStatus>(c.Rapid.ExecutionStatus.ToString()),
    };

    /// <summary>把底層例外包成 <see cref="AbbRobotException"/>；已是該型別則原樣回傳。</summary>
    private static AbbRobotException Wrap(string method, AbbRobotErrorKind kind, Exception ex)
        => ex as AbbRobotException
           ?? new AbbRobotException(kind, $"[{method}] {ex.Message}", ex);

    private void StartMonitoring()
    {
        _onConnectionChanged      = (_, _) => FireStatus();
        _onOperatingModeChanged   = (_, _) => FireStatus();
        _onStateChanged           = (_, _) => FireStatus();
        _onExecutionStatusChanged = (_, _) => FireStatus();

        _controller!.ConnectionChanged               += _onConnectionChanged;
        _controller!.OperatingModeChanged            += _onOperatingModeChanged;
        _controller!.StateChanged                    += _onStateChanged;
        _controller!.Rapid.ExecutionStatusChanged    += _onExecutionStatusChanged;
    }

    private void StopMonitoring()
    {
        var c = _controller;
        if (c == null) return;
        c.ConnectionChanged               -= _onConnectionChanged;
        c.OperatingModeChanged            -= _onOperatingModeChanged;
        c.StateChanged                    -= _onStateChanged;
        c.Rapid.ExecutionStatusChanged    -= _onExecutionStatusChanged;
        _onConnectionChanged      = null;
        _onOperatingModeChanged   = null;
        _onStateChanged           = null;
        _onExecutionStatusChanged = null;
    }

    private void FireStatus()
    {
        var ctrl = _controller;
        if (ctrl == null) return;
        try
        {
            StatusChanged?.Invoke(this, BuildStatus(ctrl));
        }
        catch {
            // 斷線後讀取其他屬性的競態例外 : 仍發射 Disconnected 確保 UI 狀態更新
            try { StatusChanged?.Invoke(this, AbbRobotStatus.Disconnected); } catch { } 
        }
    }

    /// <summary>釋放目前的 <see cref="Controller"/>（先登出再 Dispose），欄位歸 null。可重入。</summary>
    private void DisposeController()
    {
        Controller? c = _controller;
        _controller = null;
        if (c is null) return;

        try { c.Logoff(); }
        catch { /* 登出失敗不影響後續釋放 */ }
        c.Dispose();
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}
