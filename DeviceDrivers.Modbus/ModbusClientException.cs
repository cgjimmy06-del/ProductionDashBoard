using DeviceDrivers.Modbus.Models;

namespace DeviceDrivers.Modbus;

/// <summary>
/// Modbus 操作失敗一律以本例外回報；包裝底層的 NModbus / Socket / IO 例外，
/// 讓呼叫端永遠面對單一例外型別、不漏出第三方型別。
/// 透過 <see cref="Kind"/> 區分「該重連」（如 <see cref="ModbusErrorKind.ConnectionFailed"/>）
/// 與「真錯誤」（如 <see cref="ModbusErrorKind.OperationFailed"/>）。
/// </summary>
public sealed class ModbusClientException : Exception
{
    /// <summary>建立例外。</summary>
    /// <param name="kind">錯誤分類。</param>
    /// <param name="message">錯誤訊息，慣例上以 <c>[方法名]</c> 前綴。</param>
    /// <param name="innerException">被包裝的底層例外（若有）。</param>
    public ModbusClientException(ModbusErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>錯誤分類。</summary>
    public ModbusErrorKind Kind { get; }
}
