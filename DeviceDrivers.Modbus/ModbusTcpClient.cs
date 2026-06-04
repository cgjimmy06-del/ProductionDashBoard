using System.Net.Sockets;
using DeviceDrivers.Modbus.Models;
using NModbus;

namespace DeviceDrivers.Modbus;

/// <summary>
/// <see cref="IModbusClient"/> 的 TCP 實作，包裝 NModbus <see cref="IModbusMaster"/>。
/// <para>
/// 離線韌性：每個公開方法都把底層 NModbus / Socket / IO 例外包成 <see cref="ModbusClientException"/>，
/// 永不漏出原生例外、永不讓進程崩潰。連線中斷後呼叫端可再次 <see cref="Connect"/> 重連。
/// </para>
/// <para>
/// 記憶體管理：<see cref="TcpClient"/> 與 <see cref="IModbusMaster"/> 皆為 <see cref="IDisposable"/>，
/// 本類別一律於 <see cref="Disconnect"/> 時釋放；重連前先釋放舊連線，反覆連線不會洩漏。
/// </para>
/// <para>
/// <b>執行緒安全</b>：所有 public 方法皆以 <c>_gate</c> lock 序列化，可安全地由不同執行緒呼叫。
/// </para>
/// </summary>
public sealed class ModbusTcpClient : IModbusClient
{
    private const int ConnectTimeoutMs = 3000;
    private const int ReadTimeoutMs = 2000;

    private readonly string _ipAddress;
    private readonly int _port;
    private readonly byte _unitId;

    private TcpClient? _tcp;
    private IModbusMaster? _master;
    private bool _disposed;

    /// <summary>序列化所有觸及底層 TcpClient / IModbusMaster 的操作。</summary>
    private readonly object _gate = new();

    /// <summary>建立 Modbus TCP 用戶端。</summary>
    /// <param name="ipAddress">設備 IP 位址。</param>
    /// <param name="port">TCP 連接埠，預設 502。</param>
    /// <param name="unitId">Modbus Unit ID，預設 1。</param>
    public ModbusTcpClient(string ipAddress, int port = 502, byte unitId = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        _ipAddress = ipAddress;
        _port      = port;
        _unitId    = unitId;
    }

    /// <inheritdoc/>
    public bool IsConnected => !_disposed && _tcp is { Connected: true } && _master != null;

    /// <inheritdoc/>
    public void Connect()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            try
            {
                Disconnect();   // 重連前先釋放舊連線，避免洩漏

                var tcp = new TcpClient();
                IAsyncResult ar = tcp.BeginConnect(_ipAddress, _port, null, null);
                bool connected = ar.AsyncWaitHandle.WaitOne(ConnectTimeoutMs);
                if (!connected)
                {
                    try { tcp.Close(); } catch { }
                    throw new TimeoutException($"連線至 {_ipAddress}:{_port} 逾時（{ConnectTimeoutMs}ms）。");
                }
                tcp.EndConnect(ar);

                IModbusMaster master = new ModbusFactory().CreateMaster(tcp);
                master.Transport.ReadTimeout = ReadTimeoutMs;

                _tcp    = tcp;
                _master = master;
            }
            catch (ModbusClientException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw Wrap(nameof(Connect), ModbusErrorKind.ConnectionFailed, ex);
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
                DisposeTcp();
            }
            catch { /* 斷線容錯：本來就未連線也不拋例外 */ }
        }
    }

    /// <inheritdoc/>
    public bool[] ReadCoils(ushort startAddress, ushort count)
    {
        lock (_gate)
        {
            RequireConnected(nameof(ReadCoils));
            return Execute(nameof(ReadCoils),
                () => _master!.ReadCoils(_unitId, startAddress, count));
        }
    }

    /// <inheritdoc/>
    public bool[] ReadDiscreteInputs(ushort startAddress, ushort count)
    {
        lock (_gate)
        {
            RequireConnected(nameof(ReadDiscreteInputs));
            return Execute(nameof(ReadDiscreteInputs),
                () => _master!.ReadInputs(_unitId, startAddress, count));
        }
    }

    /// <inheritdoc/>
    public ushort[] ReadHoldingRegisters(ushort startAddress, ushort count)
    {
        lock (_gate)
        {
            RequireConnected(nameof(ReadHoldingRegisters));
            return Execute(nameof(ReadHoldingRegisters),
                () => _master!.ReadHoldingRegisters(_unitId, startAddress, count));
        }
    }

    /// <inheritdoc/>
    public ushort[] ReadInputRegisters(ushort startAddress, ushort count)
    {
        lock (_gate)
        {
            RequireConnected(nameof(ReadInputRegisters));
            return Execute(nameof(ReadInputRegisters),
                () => _master!.ReadInputRegisters(_unitId, startAddress, count));
        }
    }

    /// <inheritdoc/>
    public void WriteSingleCoil(ushort address, bool value)
    {
        lock (_gate)
        {
            RequireConnected(nameof(WriteSingleCoil));
            Execute(nameof(WriteSingleCoil),
                () => _master!.WriteSingleCoil(_unitId, address, value));
        }
    }

    /// <inheritdoc/>
    public void WriteMultipleCoils(ushort startAddress, bool[] values)
    {
        lock (_gate)
        {
            RequireConnected(nameof(WriteMultipleCoils));
            Execute(nameof(WriteMultipleCoils),
                () => _master!.WriteMultipleCoils(_unitId, startAddress, values));
        }
    }

    /// <inheritdoc/>
    public void WriteSingleRegister(ushort address, ushort value)
    {
        lock (_gate)
        {
            RequireConnected(nameof(WriteSingleRegister));
            Execute(nameof(WriteSingleRegister),
                () => _master!.WriteSingleRegister(_unitId, address, value));
        }
    }

    /// <inheritdoc/>
    public void WriteMultipleRegisters(ushort startAddress, ushort[] values)
    {
        lock (_gate)
        {
            RequireConnected(nameof(WriteMultipleRegisters));
            Execute(nameof(WriteMultipleRegisters),
                () => _master!.WriteMultipleRegisters(_unitId, startAddress, values));
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
                DisposeTcp();
            }
            catch { /* Dispose 不拋例外 */ }
        }
    }

    // --- 內部輔助 -----------------------------------------------------------------

    /// <summary>執行讀寫操作，依例外型別決定是否斷線，並包裝成 <see cref="ModbusClientException"/>。</summary>
    private T Execute<T>(string method, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (SlaveException ex)
        {
            // 協定層錯誤（如非法位址/功能）—— 連線保持，不斷線
            throw Wrap(method, ModbusErrorKind.OperationFailed, ex);
        }
        catch (IOException ex) when (ex.InnerException is not SocketException)
        {
            // NModbus response 驗證失敗（ValidateResponse 拋 IOException，無 inner exception）
            // 如台達 PLC 對 FC06 的非標準 response 格式。
            // 寫入/讀取已在設備端執行完畢，TCP 連線本身仍活著，不觸發斷線。
            //
            // 區分依據：NetworkStream 傳輸層斷線時拋的 IOException 其 InnerException 為 SocketException；
            // ValidateResponse 拋的 IOException 沒有 inner exception，因此可安全區分。
            throw Wrap(method, ModbusErrorKind.OperationFailed, ex);
        }
        catch (Exception ex)
        {
            // 傳輸層錯誤（SocketException / TimeoutException 等）—— 先斷線
            Disconnect();
            throw Wrap(method, ModbusErrorKind.ConnectionFailed, ex);
        }
    }

    /// <summary>Execute 的 void 版本。</summary>
    private void Execute(string method, Action action)
        => Execute<object?>(method, () => { action(); return null; });

    private void RequireConnected(string method)
    {
        ThrowIfDisposed();
        if (!IsConnected)
        {
            throw new ModbusClientException(
                ModbusErrorKind.NotConnected, $"[{method}] 尚未連線至設備。");
        }
    }

    /// <summary>把底層例外包成 <see cref="ModbusClientException"/>；已是該型別則原樣回傳。</summary>
    private static ModbusClientException Wrap(string method, ModbusErrorKind kind, Exception ex)
        => ex as ModbusClientException
           ?? new ModbusClientException(kind, $"[{method}] {ex.Message}", ex);

    /// <summary>釋放 <see cref="IModbusMaster"/> 與 <see cref="TcpClient"/>，欄位歸 null。可重入。</summary>
    private void DisposeTcp()
    {
        IModbusMaster? master = _master;
        _master = null;
        TcpClient? tcp = _tcp;
        _tcp = null;

        try { master?.Dispose(); } catch { }
        try { tcp?.Close(); } catch { }
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}
