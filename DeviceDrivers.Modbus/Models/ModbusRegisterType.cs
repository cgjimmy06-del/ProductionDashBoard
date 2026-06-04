namespace DeviceDrivers.Modbus.Models;

/// <summary>
/// Modbus 暫存器型別，對應四種讀取功能碼（FC01/02/03/04）。
/// </summary>
public enum ModbusRegisterType
{
    Coil,            // FC01 — 可讀寫
    DiscreteInput,   // FC02 — 唯讀
    HoldingRegister, // FC03 — 可讀寫
    InputRegister,   // FC04 — 唯讀
}
