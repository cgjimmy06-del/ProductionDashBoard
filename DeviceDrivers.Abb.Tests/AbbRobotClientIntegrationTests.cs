using System;
using System.Linq;
using DeviceDrivers.Abb;
using Xunit;

namespace DeviceDrivers.Abb.Tests;

/// <summary>
/// 整合測試（需連真機）。預設被 <c>--filter "Category!=Integration"</c> 排除；
/// 須在已連接 ABB 機器人的開發機上、設定環境變數後執行：
/// <list type="bullet">
///   <item><c>ABB_ROBOT_IP</c> —— 機器人 IP（必要，未設定則所有整合測試直接早退）。</item>
///   <item><c>ABB_TEST_BOOL_VAR</c> / <c>ABB_TEST_NUM_VAR</c> / <c>ABB_TEST_STRING_VAR</c>
///         —— 可「寫入」的測試變數位址，格式 <c>task/module/variable</c>，會做寫入→讀回→還原
///         （未設定則該項早退）。</item>
///   <item><c>ABB_TEST_BOOL_READ_VAR</c> / <c>ABB_TEST_STRING_READ_VAR</c>
///         —— 僅供「唯讀」驗證的現有變數位址，只讀不寫、無破壞性（未設定則該項早退）。</item>
/// </list>
/// 執行：<c>dotnet test --filter "Category=Integration"</c>
/// <para>
/// 註：xUnit v2 無動態 skip，環境未就緒時測試以早退（return）處理 —— 形式上計為通過，
/// 實際只有環境齊備時才有驗證意義。
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class AbbRobotClientIntegrationTests
{
    private static string? RobotIp => Environment.GetEnvironmentVariable("ABB_ROBOT_IP");

    private static bool HasRobot => !string.IsNullOrWhiteSpace(RobotIp);

    private static RapidVariableAddress? TestAddress(string envName)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var parts = raw.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3
            ? new RapidVariableAddress(parts[0], parts[1], parts[2])
            : throw new FormatException($"環境變數 {envName} 格式應為 task/module/variable，實得：{raw}");
    }

    /// <summary>探索 + 連線，回傳一個已連線的 client。</summary>
    private static AbbRobotClient Connected()
    {
        var client = new AbbRobotClient();
        var found = client.DiscoverControllers(RobotIp!);
        var target = found.First(c => string.Equals(c.IpAddress, RobotIp, StringComparison.OrdinalIgnoreCase));
        client.Connect(target);
        return client;
    }

    [Fact]
    public void DiscoverControllers_WithIpHint_FindsConfiguredRobot()
    {
        if (!HasRobot) return;
        using var client = new AbbRobotClient();

        var found = client.DiscoverControllers(RobotIp!);

        Assert.Contains(found, c =>
            string.Equals(c.IpAddress, RobotIp, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Connect_GetStatus_GetTasks_Disconnect_Succeed()
    {
        if (!HasRobot) return;
        using var client = Connected();

        Assert.True(client.IsConnected);

        var status = client.GetStatus();
        Assert.True(status.IsConnected);
        Assert.NotEqual(AbbControllerState.Unknown, status.State);

        var tasks = client.GetTasks();
        Assert.NotEmpty(tasks);

        client.Disconnect();
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Reconnect_AfterDisconnect_Succeeds()
    {
        if (!HasRobot) return;
        using var client = Connected();
        client.Disconnect();

        var found = client.DiscoverControllers(RobotIp!);
        var target = found.First(c =>
            string.Equals(c.IpAddress, RobotIp, StringComparison.OrdinalIgnoreCase));
        client.Connect(target);

        Assert.True(client.IsConnected);
    }

    [Fact]
    public void ReadWriteBool_RoundTrips()
    {
        if (!HasRobot) return;
        var address = TestAddress("ABB_TEST_BOOL_VAR");
        if (address is null) return;   // 未設定 ABB_TEST_BOOL_VAR，略過

        using var client = Connected();
        bool original = client.ReadBool(address);
        try
        {
            client.WriteBool(address, !original);
            Assert.Equal(!original, client.ReadBool(address));
        }
        finally
        {
            client.WriteBool(address, original);   // 還原
        }
    }

    [Fact]
    public void ReadWriteNum_RoundTrips()
    {
        if (!HasRobot) return;
        var address = TestAddress("ABB_TEST_NUM_VAR");
        if (address is null) return;   // 未設定 ABB_TEST_NUM_VAR，略過

        using var client = Connected();
        double original = client.ReadNum(address);
        try
        {
            client.WriteNum(address, original + 1);
            Assert.Equal(original + 1, client.ReadNum(address), precision: 3);
        }
        finally
        {
            client.WriteNum(address, original);   // 還原
        }
    }

    [Fact]
    public void ReadWriteString_RoundTrips()
    {
        if (!HasRobot) return;
        var address = TestAddress("ABB_TEST_STRING_VAR");
        if (address is null) return;   // 未設定 ABB_TEST_STRING_VAR，略過

        using var client = Connected();
        string original = client.ReadString(address);
        try
        {
            client.WriteString(address, "abbtest");
            Assert.Equal("abbtest", client.ReadString(address));
        }
        finally
        {
            client.WriteString(address, original);   // 還原
        }
    }

    [Fact]
    public void ReadBool_FromExistingVariable_Succeeds()
    {
        if (!HasRobot) return;
        var address = TestAddress("ABB_TEST_BOOL_READ_VAR");
        if (address is null) return;   // 未設定 ABB_TEST_BOOL_READ_VAR，略過

        using var client = Connected();
        // 唯讀、無破壞性：驗證 (Bool)rd.Value 讀取轉型 —— 轉型錯誤會丟 AbbRobotException 使測試失敗。
        bool value = client.ReadBool(address);
        Assert.IsType<bool>(value);
    }

    [Fact]
    public void ReadString_FromExistingVariable_Succeeds()
    {
        if (!HasRobot) return;
        var address = TestAddress("ABB_TEST_STRING_READ_VAR");
        if (address is null) return;   // 未設定 ABB_TEST_STRING_READ_VAR，略過

        using var client = Connected();
        // 唯讀、無破壞性：驗證 (RapidString)rd.Value 讀取轉型。
        string value = client.ReadString(address);
        Assert.NotNull(value);
    }
}
