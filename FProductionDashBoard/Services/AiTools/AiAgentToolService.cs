using System.Text.Json;
using System.Text.Json.Nodes;

namespace FProductionDashBoard.Services.AiTools;

public class AiAgentToolService
{
    private readonly IReadOnlyList<AiToolDefinition> _scheduleTools;

    public AiAgentToolService(DashboardCoreServices core)
    {
        _scheduleTools =
        [
            new AiToolDefinition(
                Name: "query_schedules",
                Description: "查詢生產排單資料，支援依日期、狀態、產品關鍵字篩選",
                Parameters: BuildQuerySchedulesSchema(),
                Handler: args => QuerySchedulesAsync(core, args))
        ];
    }

    public IReadOnlyList<AiToolDefinition> GetToolsForMode(string mode) => mode switch
    {
        "Schedule" => _scheduleTools,
        _ => []
    };

    public string GetSystemPromptForMode(string mode) => mode switch
    {
        "Schedule" =>
            "你是工廠現場生產管理系統的 AI 助理，專門協助排單分析。" +
            "回答前請先使用 query_schedules 工具取得最新資料，再以白話整理分析結果。" +
            "不可憑空猜測數字或排單狀態。" +
            "預設以繁體中文（台灣）回覆；若使用者以其他語言提問，則以相同語言回覆。簡潔易懂。",
        _ =>
            "你是工廠現場生產管理系統的 AI 助理。" +
            "協助操作人員解答與生產製程、品質、設備相關問題，簡潔易懂，避免使用技術術語。" +
            "預設以繁體中文（台灣）回覆；若使用者以其他語言提問，則以相同語言回覆。"
    };

    private static JsonObject BuildQuerySchedulesSchema() => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "dateFrom":       { "type": "string",  "description": "開始日期 YYYY-MM-DD，比對 CreateAt" },
                "dateTo":         { "type": "string",  "description": "結束日期 YYYY-MM-DD" },
                "status":         { "type": "string",  "enum": ["Pending","Scheduled","Completed","Released","Cancelled"], "description": "排單狀態" },
                "productKeyword": { "type": "string",  "description": "產品名稱或件號模糊比對" },
                "limit":          { "type": "integer", "description": "回傳筆數上限，預設 50，最大 100" }
            },
            "required": []
        }
        """)!.AsObject();

    private static async Task<string> QuerySchedulesAsync(DashboardCoreServices core, JsonObject args)
    {
        if (!core.Authorization.HasPermission(PermissionId.Schedule))
            return """{"error":"no_permission"}""";

        var all = await core.Data.GetAllSchedulesAsync();

        DateOnly? dateFrom = TryParseDate(args["dateFrom"]?.GetValue<string>());
        DateOnly? dateTo   = TryParseDate(args["dateTo"]?.GetValue<string>());
        string?   status   = args["status"]?.GetValue<string>();
        string?   keyword  = args["productKeyword"]?.GetValue<string>();
        int       limit    = Math.Min(args["limit"]?.GetValue<int>() ?? 50, 100);

        var filtered = all.AsEnumerable();

        if (dateFrom.HasValue)
            filtered = filtered.Where(s => s.CreateAt.HasValue &&
                DateOnly.FromDateTime(s.CreateAt.Value) >= dateFrom.Value);

        if (dateTo.HasValue)
            filtered = filtered.Where(s => s.CreateAt.HasValue &&
                DateOnly.FromDateTime(s.CreateAt.Value) <= dateTo.Value);

        if (!string.IsNullOrEmpty(status) &&
            Enum.TryParse<Models.ScheduleStatus>(status, out var statusEnum))
            filtered = filtered.Where(s => s.Status == statusEnum);

        if (!string.IsNullOrEmpty(keyword))
            filtered = filtered.Where(s =>
                (s.Product?.Part?.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Product?.Part?.PartNo?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Product?.Model?.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));

        var results = filtered.Take(limit).Select(s => new
        {
            id          = s.ScheduleId,
            part        = s.Product?.Part?.PartNo,
            partName    = s.Product?.Part?.Name,
            model       = s.Product?.Model?.Name,
            process     = s.Process?.Name,
            status      = s.Status.ToString(),
            quantity    = s.Quantity,
            actualQty   = s.ActualQuantity,
            lotNo       = s.LotNo,
            scheduledAt = s.ScheduledAt?.ToString("yyyy-MM-dd"),
            createAt    = s.CreateAt?.ToString("yyyy-MM-dd")
        }).ToList();

        return JsonSerializer.Serialize(new { total = filtered.Count(), results });
    }

    private static DateOnly? TryParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", out var d) ? d : null;
}
