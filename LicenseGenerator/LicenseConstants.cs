namespace LicenseGenerator;

internal static class LicenseConstants
{
    internal const string HmacKey = "FbE7RvbRwJrmuqYZE+I3mKB1DtB5eP3sB+Ms+px/h2A=";

    // ⚠ 新增 LicensedFeature enum 值時，必須同步更新此清單
    internal static readonly string[] KnownFeatures =
        ["Charts", "Scheduling", "ProgramLibrary", "MaterialManagement"];
}
