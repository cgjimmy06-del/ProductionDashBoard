using DeviceDrivers.Modbus;
using DeviceDrivers.Modbus.Models;
using Xunit;

namespace DeviceDrivers.Modbus.Tests;

/// <summary>
/// 單元測試（免設備）：<see cref="ModbusTcpClient"/> 在未連線 / 已釋放狀態下的防護行為，
/// 驗證離線韌性 —— 不崩潰、不漏出原生例外。
/// </summary>
public class ModbusTcpClientGuardTests
{
    [Fact]
    public void NewClient_IsNotConnected()
    {
        using var client = new ModbusTcpClient("192.168.1.1");
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Constructor_WithEmptyIp_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ModbusTcpClient(""));
        Assert.Throws<ArgumentException>(() => new ModbusTcpClient("   "));
    }

    [Fact]
    public void ReadOperations_WhenNotConnected_ThrowNotConnected()
    {
        using var client = new ModbusTcpClient("192.168.1.1");

        Assert.Equal(ModbusErrorKind.NotConnected,
            Assert.Throws<ModbusClientException>(() => client.ReadCoils(0, 1)).Kind);
        Assert.Equal(ModbusErrorKind.NotConnected,
            Assert.Throws<ModbusClientException>(() => client.ReadDiscreteInputs(0, 1)).Kind);
        Assert.Equal(ModbusErrorKind.NotConnected,
            Assert.Throws<ModbusClientException>(() => client.ReadHoldingRegisters(0, 1)).Kind);
        Assert.Equal(ModbusErrorKind.NotConnected,
            Assert.Throws<ModbusClientException>(() => client.ReadInputRegisters(0, 1)).Kind);
    }

    [Fact]
    public void WriteOperations_WhenNotConnected_ThrowNotConnected()
    {
        using var client = new ModbusTcpClient("192.168.1.1");

        Assert.Equal(ModbusErrorKind.NotConnected,
            Assert.Throws<ModbusClientException>(() => client.WriteSingleCoil(0, true)).Kind);
        Assert.Equal(ModbusErrorKind.NotConnected,
            Assert.Throws<ModbusClientException>(() => client.WriteMultipleCoils(0, [true, false])).Kind);
        Assert.Equal(ModbusErrorKind.NotConnected,
            Assert.Throws<ModbusClientException>(() => client.WriteSingleRegister(0, 1)).Kind);
        Assert.Equal(ModbusErrorKind.NotConnected,
            Assert.Throws<ModbusClientException>(() => client.WriteMultipleRegisters(0, [1, 2])).Kind);
    }

    [Fact]
    public void ReadOperations_AfterDispose_ThrowObjectDisposedException()
    {
        var client = new ModbusTcpClient("192.168.1.1");
        client.Dispose();

        Assert.Throws<ObjectDisposedException>(() => client.ReadCoils(0, 1));
        Assert.Throws<ObjectDisposedException>(() => client.ReadHoldingRegisters(0, 1));
        Assert.Throws<ObjectDisposedException>(() => client.WriteSingleRegister(0, 1));
    }

    [Fact]
    public void Connect_AfterDispose_ThrowObjectDisposedException()
    {
        var client = new ModbusTcpClient("192.168.1.1");
        client.Dispose();

        Assert.Throws<ObjectDisposedException>(() => client.Connect());
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var client = new ModbusTcpClient("192.168.1.1");
        client.Dispose();
        client.Dispose();   // 重複 Dispose 不應拋例外
    }

    [Fact]
    public void Disconnect_WhenNeverConnected_DoesNotThrow()
    {
        using var client = new ModbusTcpClient("192.168.1.1");
        client.Disconnect();   // 不應拋例外
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Disconnect_AfterDispose_DoesNotThrow()
    {
        var client = new ModbusTcpClient("192.168.1.1");
        client.Dispose();
        client.Disconnect();   // 不應拋例外
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Connect_WithUnreachableHost_ThrowsConnectionFailed()
    {
        // 使用本機無效連接埠，確保快速連線失敗
        using var client = new ModbusTcpClient("127.0.0.1", 19999);

        var ex = Assert.Throws<ModbusClientException>(() => client.Connect());
        Assert.Equal(ModbusErrorKind.ConnectionFailed, ex.Kind);
        Assert.False(client.IsConnected);
    }
}
