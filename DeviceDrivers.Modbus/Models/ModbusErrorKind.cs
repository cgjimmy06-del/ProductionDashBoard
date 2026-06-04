namespace DeviceDrivers.Modbus.Models;

/// <summary>
/// <see cref="ModbusClientException"/> 的錯誤分類，供呼叫端分辨「該重連」與「真錯誤」。
/// </summary>
public enum ModbusErrorKind
{
    /// <summary>未分類錯誤。</summary>
    Unknown = 0,
    /// <summary>連線失敗，或讀寫時遇傳輸層斷線 —— IsConnected 轉 false，呼叫端可重連。</summary>
    ConnectionFailed,
    /// <summary>尚未連線就呼叫了需要連線的操作。</summary>
    NotConnected,
    /// <summary>協定層錯誤（如 SlaveException：非法位址/功能）—— 連線保持，不需重連。</summary>
    OperationFailed,
}
