using DeviceDrivers.Modbus;
using DeviceDrivers.Modbus.Models;
using Xunit;

namespace DeviceDrivers.Modbus.Tests;

/// <summary>
/// 整合測試（需連真實 Modbus 設備）。預設被 <c>--filter "Category!=Integration"</c> 排除；
/// 須在已連接 Modbus 設備的開發機上、設定環境變數後執行：
/// <list type="bullet">
///   <item><c>MODBUS_IP</c> —— 設備 IP（必要，未設定則所有整合測試直接早退）。</item>
///   <item><c>MODBUS_PORT</c> —— TCP 連接埠（選用，預設 502）。</item>
///   <item><c>MODBUS_UNIT_ID</c> —— Unit ID（選用，預設 1）。</item>
///   <item><c>MODBUS_HOLDING_READ_ADDR</c> —— 可讀 Holding Register 起始位址（選用）。</item>
///   <item><c>MODBUS_COIL_WRITE_ADDR</c> —— 可寫 Coil 位址（選用，寫入後還原）。</item>
///   <item><c>MODBUS_REGISTER_WRITE_ADDR</c> —— 可寫 Holding Register 位址（選用，寫入後還原）。</item>
/// </list>
/// 執行：<c>dotnet test --filter "Category=Integration"</c>
/// <para>
/// 註：xUnit v2 無動態 skip，環境未就緒時測試以早退（return）處理 —— 形式上計為通過，
/// 實際只有環境齊備時才有驗證意義。
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class ModbusTcpClientIntegrationTests
{
    private static string? ModbusIp   => Environment.GetEnvironmentVariable("MODBUS_IP");
    private static int     ModbusPort => int.TryParse(Environment.GetEnvironmentVariable("MODBUS_PORT"), out int p) ? p : 502;
    private static byte    ModbusUnit => byte.TryParse(Environment.GetEnvironmentVariable("MODBUS_UNIT_ID"), out byte u) ? u : (byte)1;
    private static bool    HasDevice  => !string.IsNullOrWhiteSpace(ModbusIp);

    private static ushort? EnvAddr(string envName)
        => ushort.TryParse(Environment.GetEnvironmentVariable(envName), out ushort addr) ? addr : null;

    private static ModbusTcpClient Connected()
    {
        var client = new ModbusTcpClient(ModbusIp!, ModbusPort, ModbusUnit);
        client.Connect();
        return client;
    }

    [Fact]
    public void Connect_Disconnect_Succeed()
    {
        if (!HasDevice) return;
        using var client = Connected();

        Assert.True(client.IsConnected);

        client.Disconnect();
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Reconnect_AfterDisconnect_Succeeds()
    {
        if (!HasDevice) return;
        using var client = Connected();
        client.Disconnect();

        client.Connect();
        Assert.True(client.IsConnected);
    }

    [Fact]
    public void ReadHoldingRegisters_Succeeds()
    {
        if (!HasDevice) return;
        ushort addr = EnvAddr("MODBUS_HOLDING_READ_ADDR") ?? 0;

        using var client = Connected();
        ushort[] result = client.ReadHoldingRegisters(addr, 1);

        Assert.Single(result);
    }

    [Fact]
    public void WriteReadSingleRegister_RoundTrips()
    {
        if (!HasDevice) return;
        ushort? addr = EnvAddr("MODBUS_REGISTER_WRITE_ADDR");
        if (addr is null) return;

        using var client = Connected();
        ushort original = client.ReadHoldingRegisters(addr.Value, 1)[0];
        try
        {
            ushort newValue = (ushort)(original == 0 ? 1 : 0);
            client.WriteSingleRegister(addr.Value, newValue);
            Assert.Equal(newValue, client.ReadHoldingRegisters(addr.Value, 1)[0]);
        }
        finally
        {
            client.WriteSingleRegister(addr.Value, original);   // 還原
        }
    }

    [Fact]
    public void WriteReadCoil_RoundTrips()
    {
        if (!HasDevice) return;
        ushort? addr = EnvAddr("MODBUS_COIL_WRITE_ADDR");
        if (addr is null) return;

        using var client = Connected();
        bool original = client.ReadCoils(addr.Value, 1)[0];
        try
        {
            client.WriteSingleCoil(addr.Value, !original);
            Assert.Equal(!original, client.ReadCoils(addr.Value, 1)[0]);
        }
        finally
        {
            client.WriteSingleCoil(addr.Value, original);   // 還原
        }
    }
}
