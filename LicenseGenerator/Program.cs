using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LicenseGenerator;

string? customer = null, expiry = null, output = null;
var features = new List<string>();

if (args.Length == 0)
{
    // 互動模式（雙擊 exe 啟動）
    var known = string.Join(", ", LicenseConstants.KnownFeatures);
    Console.WriteLine("=== LicenseGenerator 授權檔產生工具 ===\n");

    Console.Write("客戶/廠商名稱（必填）：");
    customer = Console.ReadLine()?.Trim();

    Console.WriteLine($"可用功能：{known}");
    Console.Write("開放功能（逗號分隔，輸入 all 全開，直接 Enter 則全功能鎖定）：");
    var featInput = Console.ReadLine()?.Trim();
    if (!string.IsNullOrEmpty(featInput))
        features = featInput.Split(',').Select(f => f.Trim()).ToList();

    Console.Write("到期日（格式 yyyy-MM-dd，直接 Enter 則永久有效）：");
    expiry = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(expiry)) expiry = null;

    var defaultOutput = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "license.lic");
    Console.Write("輸出路徑（直接 Enter 輸出至桌面 license.lic）：");
    output = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(output)) output = defaultOutput;

    Console.WriteLine();
}
else
{
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--customer" when i + 1 < args.Length: customer = args[++i]; break;
            case "--features" when i + 1 < args.Length: features = args[++i].Split(',').ToList(); break;
            case "--expiry"   when i + 1 < args.Length: expiry   = args[++i]; break;
            case "--output"   when i + 1 < args.Length: output   = args[++i]; break;
            case "-h":
            case "--help": PrintHelp(); return 0;
        }
    }
}

if (string.IsNullOrWhiteSpace(customer))
{
    Console.Error.WriteLine("錯誤：客戶名稱不可為空。");
    if (args.Length == 0) { Console.WriteLine("按任意鍵關閉..."); Console.ReadKey(); }
    return 1;
}

output ??= Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "license.lic");

if (features.Count == 1 && features[0].Equals("all", StringComparison.OrdinalIgnoreCase))
    features = LicenseConstants.KnownFeatures.ToList();

DateTime? expiryDate = null;
if (expiry is not null)
{
    if (!DateTime.TryParseExact(expiry, "yyyy-MM-dd", null,
        System.Globalization.DateTimeStyles.None, out var parsedDate))
    {
        Console.WriteLine($"[錯誤] 日期格式無效：{expiry}，請使用 yyyy-MM-dd");
        if (args.Length == 0) { Console.WriteLine("按任意鍵關閉..."); Console.ReadKey(); }
        return 1;
    }
    expiryDate = parsedDate;
}

var dto = new LicenseFileDto
{
    CustomerName    = customer,
    ExpiryDate      = expiryDate,
    EnabledFeatures = features,
    IssuedAt        = DateTime.Today,
    Signature       = ""
};

var featureStr = string.Join(",", dto.EnabledFeatures.OrderBy(f => f));
var expiryStr  = dto.ExpiryDate?.ToString("yyyy-MM-dd") ?? "null";
var payload    = $"{dto.CustomerName}|{expiryStr}|{dto.IssuedAt:yyyy-MM-dd}|{featureStr}";
var key        = Convert.FromBase64String(LicenseConstants.HmacKey);
using var hmac = new HMACSHA256(key);
dto = dto with { Signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))) };

var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(output, json);
Console.WriteLine($"✓ 授權檔已產生 → {Path.GetFullPath(output)}");

if (args.Length == 0) { Console.WriteLine("\n按任意鍵關閉..."); Console.ReadKey(); }
return 0;

static void PrintHelp()
{
    var known = string.Join(", ", LicenseConstants.KnownFeatures);
    Console.WriteLine($"""
    用法：
      LicenseGenerator --customer <名稱> [--features <功能清單>] [--expiry <日期>] [--output <路徑>]

    必填：
      --customer  客戶/廠商名稱（例："台灣工廠"）

    選填：
      --features  功能代碼（逗號分隔），省略則全功能鎖定
                  可用值：{known}
                  使用 all 開放全部功能（例：--features all）
      --expiry    授權到期日（格式：yyyy-MM-dd），省略則永久有效
      --output    輸出的 .lic 檔案路徑，省略則輸出至桌面 license.lic
      --help / -h 顯示此說明

    範例：
      LicenseGenerator --customer "台灣工廠" --features Charts,Scheduling --expiry 2027-12-31
      LicenseGenerator --customer "總部" --features all --output perm.lic
    """);
}
