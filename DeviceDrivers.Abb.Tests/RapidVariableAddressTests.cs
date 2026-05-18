using DeviceDrivers.Abb;
using Xunit;

namespace DeviceDrivers.Abb.Tests;

/// <summary>單元測試（免機器人）：<see cref="RapidVariableAddress"/> 的建構驗證與行為。</summary>
public class RapidVariableAddressTests
{
    [Fact]
    public void Constructor_WithValidFields_SetsProperties()
    {
        var address = new RapidVariableAddress("T_ROB1", "MainModule", "flag1");

        Assert.Equal("T_ROB1", address.Task);
        Assert.Equal("MainModule", address.Module);
        Assert.Equal("flag1", address.Variable);
    }

    [Fact]
    public void ToString_ReturnsSlashJoinedAddress()
        => Assert.Equal(
            "T_ROB1/MainModule/flag1",
            new RapidVariableAddress("T_ROB1", "MainModule", "flag1").ToString());

    [Fact]
    public void Equality_SameFields_AreEqual()
    {
        var a = new RapidVariableAddress("T", "M", "V");
        var b = new RapidVariableAddress("T", "M", "V");

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(null, "M", "V")]
    [InlineData("", "M", "V")]
    [InlineData("   ", "M", "V")]
    [InlineData("T", null, "V")]
    [InlineData("T", "", "V")]
    [InlineData("T", "   ", "V")]
    [InlineData("T", "M", null)]
    [InlineData("T", "M", "")]
    [InlineData("T", "M", "   ")]
    public void Constructor_WithBlankField_ThrowsArgumentException(
        string? task, string? module, string? variable)
        => Assert.Throws<ArgumentException>(
            () => new RapidVariableAddress(task!, module!, variable!));
}
