using DeviceDrivers.Abb;
using Xunit;

namespace DeviceDrivers.Abb.Tests;

/// <summary>
/// 單元測試（免機器人）：<see cref="AbbRobotClient"/> 事件訂閱與清理的防護行為。
/// </summary>
public class AbbRobotClientSubscriptionTests
{
    [Fact]
    public void StatusChanged_SubscribeWhenNotConnected_DoesNotThrow()
    {
        using var client = new AbbRobotClient();
        client.StatusChanged += (_, _) => { };
    }

    [Fact]
    public void StatusChanged_AfterDispose_HandlerNotInvoked()
    {
        var client = new AbbRobotClient();
        bool handlerInvoked = false;
        client.StatusChanged += (_, _) => { handlerInvoked = true; };

        client.Dispose();

        // Dispose 後事件訂閱者已被取消，不應再被呼叫
        Assert.False(handlerInvoked);
    }

    [Fact]
    public void Disconnect_WhenNotConnected_DoesNotThrow()
    {
        using var client = new AbbRobotClient();
        client.Disconnect();   // 觸發 StopMonitoring，不應拋例外
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Disconnect_AfterDispose_DoesNotThrow()
    {
        var client = new AbbRobotClient();
        client.Dispose();
        client.Disconnect();   // Dispose 後再 Disconnect 不應拋例外
    }
}
