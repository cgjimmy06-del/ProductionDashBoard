using System.Net;
using System.Net.Sockets;
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

    /// <summary>
    /// 驗證 NModbus ValidateResponse 拋出的 IOException（非 SocketException）
    /// 被歸為 OperationFailed，且不觸發斷線——模擬台達 PLC FC06 非標準 response。
    /// 伺服器對所有請求（含 NModbus 內部 retry）持續回傳錯誤 value，
    /// 確保最終 IOException 來自 ValidateResponse 而非傳輸層中斷。
    /// </summary>
    [Fact]
    public async Task WriteSingleRegister_WhenResponseValueMismatch_IsOperationFailedAndStaysConnected()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // 假 Modbus TCP server：對所有請求（含 NModbus retry）持續回傳 value 錯誤的 response
        var serverTask = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync();
            var stream = socket.GetStream();
            var buf = new byte[256];
            while (true)
            {
                int n;
                try { n = await stream.ReadAsync(buf); }
                catch { break; }
                if (n == 0) break;
                if (n < 12) continue;
                // Echo transaction ID + address，value 故意錯誤（0xDEAD），觸發 ValidateResponse IOException
                var resp = new byte[]
                {
                    buf[0], buf[1],   // Transaction ID echo
                    0x00, 0x00,       // Protocol ID
                    0x00, 0x06,       // Length = 6
                    buf[6],           // Unit ID
                    0x06,             // FC06
                    buf[8], buf[9],   // Address echo（正確）
                    0xDE, 0xAD        // Value（故意不同）
                };
                try { await stream.WriteAsync(resp); }
                catch { break; }
            }
        });

        try
        {
            using var client = new ModbusTcpClient("127.0.0.1", port);
            client.Connect();

            var ex = Assert.Throws<ModbusClientException>(
                () => client.WriteSingleRegister(0x0010, 0x1234));

            Assert.Equal(ModbusErrorKind.OperationFailed, ex.Kind);
            Assert.True(client.IsConnected); // 不應斷線
        }
        finally
        {
            listener.Stop();
        }

        await serverTask;
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
