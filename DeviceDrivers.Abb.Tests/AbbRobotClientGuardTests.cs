using System;
using DeviceDrivers.Abb;
using Xunit;

namespace DeviceDrivers.Abb.Tests;

/// <summary>
/// 單元測試（免機器人）：<see cref="AbbRobotClient"/> 在未連線 / 已釋放狀態下的防護行為，
/// 驗證離線韌性 —— 不崩潰、不漏出原生例外。
/// </summary>
public class AbbRobotClientGuardTests
{
    private static readonly RapidVariableAddress SampleAddress = new("T_ROB1", "Module1", "var1");

    [Fact]
    public void NewClient_IsNotConnected()
    {
        using var client = new AbbRobotClient();
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Connect_WithNull_ThrowsArgumentNullException()
    {
        using var client = new AbbRobotClient();
        Assert.Throws<ArgumentNullException>(() => client.Connect(null!));
    }

    [Fact]
    public void GetStatus_WhenNotConnected_ThrowsNotConnected()
    {
        using var client = new AbbRobotClient();

        var ex = Assert.Throws<AbbRobotException>(() => client.GetStatus());
        Assert.Equal(AbbRobotErrorKind.NotConnected, ex.Kind);
    }

    [Fact]
    public void GetTasks_WhenNotConnected_ThrowsNotConnected()
    {
        using var client = new AbbRobotClient();

        var ex = Assert.Throws<AbbRobotException>(() => client.GetTasks());
        Assert.Equal(AbbRobotErrorKind.NotConnected, ex.Kind);
    }

    [Fact]
    public void ReadWriteOperations_WhenNotConnected_ThrowNotConnected()
    {
        using var client = new AbbRobotClient();

        Assert.Equal(AbbRobotErrorKind.NotConnected,
            Assert.Throws<AbbRobotException>(() => client.ReadBool(SampleAddress)).Kind);
        Assert.Equal(AbbRobotErrorKind.NotConnected,
            Assert.Throws<AbbRobotException>(() => client.ReadString(SampleAddress)).Kind);
        Assert.Equal(AbbRobotErrorKind.NotConnected,
            Assert.Throws<AbbRobotException>(() => client.ReadNum(SampleAddress)).Kind);
        Assert.Equal(AbbRobotErrorKind.NotConnected,
            Assert.Throws<AbbRobotException>(() => client.WriteBool(SampleAddress, true)).Kind);
        Assert.Equal(AbbRobotErrorKind.NotConnected,
            Assert.Throws<AbbRobotException>(() => client.WriteString(SampleAddress, "x")).Kind);
        Assert.Equal(AbbRobotErrorKind.NotConnected,
            Assert.Throws<AbbRobotException>(() => client.WriteNum(SampleAddress, 1.0)).Kind);
    }

    [Fact]
    public void ReadOperations_WithNullAddress_ThrowArgumentNullException()
    {
        using var client = new AbbRobotClient();
        Assert.Throws<ArgumentNullException>(() => client.ReadBool(null!));
        Assert.Throws<ArgumentNullException>(() => client.WriteNum(null!, 0));
    }

    [Fact]
    public void Disconnect_WhenNeverConnected_DoesNotThrow()
    {
        using var client = new AbbRobotClient();
        client.Disconnect();   // 不應拋例外
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var client = new AbbRobotClient();
        client.Dispose();
        client.Dispose();   // 重複 Dispose 不應拋例外
    }

    [Fact]
    public void Disconnect_AfterDispose_DoesNotThrow()
    {
        var client = new AbbRobotClient();
        client.Dispose();
        client.Disconnect();   // 不應拋例外
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void DiscoverControllers_AfterDispose_ThrowsObjectDisposed()
    {
        var client = new AbbRobotClient();
        client.Dispose();
        Assert.Throws<ObjectDisposedException>(() => client.DiscoverControllers());
    }

    [Fact]
    public void GetStatus_AfterDispose_ThrowsObjectDisposed()
    {
        var client = new AbbRobotClient();
        client.Dispose();
        Assert.Throws<ObjectDisposedException>(() => client.GetStatus());
    }

    [Fact]
    public void DiscoverControllers_NeverLeaksRawException()
    {
        // 不論本機是否安裝 ABB PC SDK：探索若失敗，必須是 AbbRobotException，
        // 絕不漏出原生 COM/SDK 例外。成功則回傳清單（可能為空）。
        using var client = new AbbRobotClient();
        try
        {
            var result = client.DiscoverControllers();
            Assert.NotNull(result);
        }
        catch (Exception ex)
        {
            Assert.IsType<AbbRobotException>(ex);
        }
    }
}
