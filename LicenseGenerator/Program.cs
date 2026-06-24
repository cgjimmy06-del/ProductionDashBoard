using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LicenseGenerator;

string? customer = null, expiry = null, output = null;
var features = new List<string>();

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

if (customer is null)
{
    Console.Error.WriteLine("缺少必要參數 --customer，請使用 --help 查看用法。");
    return 1;
}

output ??= Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "license.lic");

if (features.Count == 1 && features[0].Equals("all", StringComparison.OrdinalIgnoreCase))
    features = LicenseConstants.KnownFeatures.ToList();

var dto = new LicenseFileDto
{
    CustomerName    = customer,
    ExpiryDate      = expiry is not null ? DateTime.Parse(expiry) : (DateTime?)null,
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
