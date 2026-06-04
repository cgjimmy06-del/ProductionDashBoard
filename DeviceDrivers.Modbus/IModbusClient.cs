namespace DeviceDrivers.Modbus;

/// <summary>
/// Modbus 設備用戶端。提供 TCP 連線管理與 Coil / Register 讀寫操作。
/// <para>
/// 所有失敗一律以 <see cref="ModbusClientException"/> 回報，不漏出 NModbus / Socket 型別；
/// 連線失敗不會讓進程崩潰，呼叫端可持續運行並隨時再次 <see cref="Connect"/> 重連。
/// </para>
/// <para>
/// <b>執行緒安全</b>：所有 public 方法皆以內部 lock 序列化，可安全地由不同執行緒呼叫。
/// </para>
/// </summary>
public interface IModbusClient : IDisposable
{
    /// <summary>目前是否已連線至設備。此屬性不會拋出例外，可隨時輪詢。</summary>
    bool IsConnected { get; }

    /// <summary>
    /// 連線至設備（以建構子傳入的 IP / Port / UnitId）。
    /// 重連前會先釋放舊連線，反覆連線不會洩漏資源。
    /// </summary>
    /// <exception cref="ModbusClientException">連線失敗，Kind = ConnectionFailed。</exception>
    void Connect();

    /// <summary>中斷連線。對「本來就未連線」的情況容錯，不拋例外。</summary>
    void Disconnect();

    /// <summary>讀取 Coil（FC01）。</summary>
    /// <exception cref="ModbusClientException">未連線（NotConnected）或操作失敗。</exception>
    bool[] ReadCoils(ushort startAddress, ushort count);

    /// <summary>讀取 Discrete Input（FC02）。</summary>
    /// <exception cref="ModbusClientException">未連線（NotConnected）或操作失敗。</exception>
    bool[] ReadDiscreteInputs(ushort startAddress, ushort count);

    /// <summary>讀取 Holding Register（FC03），回傳原始 16-bit word。</summary>
    /// <exception cref="ModbusClientException">未連線（NotConnected）或操作失敗。</exception>
    ushort[] ReadHoldingRegisters(ushort startAddress, ushort count);

    /// <summary>讀取 Input Register（FC04），回傳原始 16-bit word。</summary>
    /// <exception cref="ModbusClientException">未連線（NotConnected）或操作失敗。</exception>
    ushort[] ReadInputRegisters(ushort startAddress, ushort count);

    /// <summary>寫入單一 Coil（FC05）。</summary>
    /// <exception cref="ModbusClientException">未連線（NotConnected）或操作失敗。</exception>
    void WriteSingleCoil(ushort address, bool value);

    /// <summary>寫入多個 Coil（FC15）。</summary>
    /// <exception cref="ModbusClientException">未連線（NotConnected）或操作失敗。</exception>
    void WriteMultipleCoils(ushort startAddress, bool[] values);

    /// <summary>寫入單一 Holding Register（FC06）。</summary>
    /// <exception cref="ModbusClientException">未連線（NotConnected）或操作失敗。</exception>
    void WriteSingleRegister(ushort address, ushort value);

    /// <summary>寫入多個 Holding Register（FC16）。</summary>
    /// <exception cref="ModbusClientException">未連線（NotConnected）或操作失敗。</exception>
    void WriteMultipleRegisters(ushort startAddress, ushort[] values);
}
