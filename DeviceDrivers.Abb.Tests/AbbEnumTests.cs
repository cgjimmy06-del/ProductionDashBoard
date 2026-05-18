using DeviceDrivers.Abb;
using Xunit;

namespace DeviceDrivers.Abb.Tests;

/// <summary>
/// 單元測試（免機器人）：確認狀態/錯誤 enum 的 <c>Unknown</c> 皆為 0。
/// 這是離線韌性設計的前提 —— SDK enum 對映失敗時回 <c>default</c>，必須等同 <c>Unknown</c>。
/// </summary>
public class AbbEnumTests
{
    [Fact]
    public void StatusEnums_DefaultValueIsUnknown()
    {
        Assert.Equal(AbbControllerState.Unknown, default(AbbControllerState));
        Assert.Equal(AbbOperatingMode.Unknown, default(AbbOperatingMode));
        Assert.Equal(AbbExecutionStatus.Unknown, default(AbbExecutionStatus));
        Assert.Equal(AbbRobotErrorKind.Unknown, default(AbbRobotErrorKind));
    }

    [Fact]
    public void StatusEnums_UnknownIsZero()
    {
        Assert.Equal(0, (int)AbbControllerState.Unknown);
        Assert.Equal(0, (int)AbbOperatingMode.Unknown);
        Assert.Equal(0, (int)AbbExecutionStatus.Unknown);
        Assert.Equal(0, (int)AbbRobotErrorKind.Unknown);
    }
}
