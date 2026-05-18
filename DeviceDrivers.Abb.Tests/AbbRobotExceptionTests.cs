using System;
using DeviceDrivers.Abb;
using Xunit;

namespace DeviceDrivers.Abb.Tests;

/// <summary>單元測試（免機器人）：<see cref="AbbRobotException"/> 的建構行為。</summary>
public class AbbRobotExceptionTests
{
    [Fact]
    public void Constructor_SetsKindAndMessage()
    {
        var ex = new AbbRobotException(AbbRobotErrorKind.NotConnected, "[GetStatus] 尚未連線。");

        Assert.Equal(AbbRobotErrorKind.NotConnected, ex.Kind);
        Assert.Equal("[GetStatus] 尚未連線。", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Constructor_PreservesInnerException()
    {
        var inner = new InvalidOperationException("原生錯誤");

        var ex = new AbbRobotException(AbbRobotErrorKind.ConnectionFailed, "[Connect] 失敗", inner);

        Assert.Equal(AbbRobotErrorKind.ConnectionFailed, ex.Kind);
        Assert.Same(inner, ex.InnerException);
    }
}
