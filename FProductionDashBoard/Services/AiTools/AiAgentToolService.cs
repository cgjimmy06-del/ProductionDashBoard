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
                Handler: args => QuerySchedulesAsync(core, args)),
            new AiToolDefinition(
                Name: "query_orders",
                Description: "查詢訂單的生產狀態，可依設備或狀態篩選",
                Parameters: BuildQueryOrdersSchema(),
                Handler: args => QueryOrdersAsync(core, args)),
            new AiToolDefinition(
                Name: "query_equipment_status",
                Description: "查詢各設備當前是否在生產，以及生產中的品項資訊",
                Parameters: BuildEmptySchema(),
                Handler: args => QueryEquipmentStatusAsync(core, args)),
            new AiToolDefinition(
                Name: "query_tuning_status",
                Description: "查詢目前進行中的調試記錄",
                Parameters: BuildEmptySchema(),
                Handler: args => QueryTuningStatusAsync(core, args)),
            new AiToolDefinition(
                Name: "query_equipment_capabilities",
                Description: "查詢哪些機台能生產指定品項，可依件號、型號或製程關鍵字篩選",
                Parameters: BuildQueryEquipmentCapabilitiesSchema(),
                Handler: args => QueryEquipmentCapabilitiesAsync(core, args))
        ];
    }

    public IReadOnlyList<AiToolDefinition> GetToolsForMode(string mode) => mode switch
    {
        AiAgentMode.Schedule => _scheduleTools,
        _ => []
    };

    public string GetSystemPromptForMode(string mode) => mode switch
    {
        AiAgentMode.Schedule =>
            "你是工廠現場生產管理系統的 AI 助理，負責查詢與呈現現場生產資料。" +
            "回答問題前請先選擇適合的工具取得最新資料，再以白話整理呈現：" +
            "查排單用 query_schedules；查訂單用 query_orders；查設備當前生產用 query_equipment_status；查調試用 query_tuning_status；查機台可生產品項用 query_equipment_capabilities。" +
            "回應以資料事實為主；若問題超出現有資料範圍，說明資料不足，不推測或給出排單建議。" +
            "不可憑空猜測數字或狀態。" +
            "預設以繁體中文（台灣）回覆；若使用者以其他語言提問，則以相同語言回覆。簡潔易懂。",
        _ =>
            "你是工廠現場生產管理系統的 AI 助理。" +
            "協助操作人員解答與生產製程、品質、設備相關問題，簡潔易懂，避免使用技術術語。" +
            "預設以繁體中文（台灣）回覆；若使用者以其他語言提問，則以相同語言回覆。"
    };

    // ── query_schedules ───────────────────────────────────────────────────────

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
        try
        {
            if (!core.Authorization.HasPermission(PermissionId.Schedule))
                return """{"error":"no_permission"}""";

            var all = await core.Data.GetAllSchedulesAsync();

            DateOnly? dateFrom = TryParseDate(args["dateFrom"]?.GetValue<string>());
            DateOnly? dateTo   = TryParseDate(args["dateTo"]?.GetValue<string>());
            string?   status   = args["status"]?.GetValue<string>();
            string?   keyword  = args["productKeyword"]?.GetValue<string>();
            int limit = 50;
            if (args["limit"] is JsonValue lv)
            {
                if (!lv.TryGetValue<int>(out limit))
                    int.TryParse(lv.ToString(), out limit);
            }
            limit = Math.Min(limit, 100);

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

            var filteredList = filtered.ToList();
            var results = filteredList.Take(limit).Select(s => new
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

            return JsonSerializer.Serialize(new { total = filteredList.Count, results });
        }
        catch (Exception ex)
        {
            core.Log.AddErrorLog($"[QuerySchedulesAsync] {ex.Message}");
            return """{"error":"tool_failed"}""";
        }
    }

    // ── query_orders ──────────────────────────────────────────────────────────

    private static JsonObject BuildQueryOrdersSchema() => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "status":       { "type": "string",  "enum": ["Pending","InProduction","Completed","Cancelled"], "description": "訂單狀態" },
                "equipment_id": { "type": "integer", "description": "設備 ID，只回傳指定設備的訂單" },
                "limit":        { "type": "integer", "description": "回傳筆數上限，預設 20，最大 50" }
            },
            "required": []
        }
        """)!.AsObject();

    private static async Task<string> QueryOrdersAsync(DashboardCoreServices core, JsonObject args)
    {
        if (!core.Authorization.HasPermission(PermissionId.Order))
            return """{"error":"no_permission"}""";
        try
        {
            var all = await core.Data.GetAllOrderProductionsAsync();

            string? status      = args["status"]?.GetValue<string>();
            int?    equipmentId = null;
            if (args["equipment_id"] is JsonValue ev && ev.TryGetValue<int>(out var eid))
                equipmentId = eid;
            int limit = 20;
            if (args["limit"] is JsonValue lv)
            {
                if (!lv.TryGetValue<int>(out limit))
                    int.TryParse(lv.ToString(), out limit);
            }
            limit = Math.Min(limit, 50);

            var filtered = all.AsEnumerable();

            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<Models.OrderProductionStatus>(status, out var statusEnum))
                    return """{"error":"invalid_status"}""";
                filtered = filtered.Where(o => o.Status == statusEnum);
            }

            if (equipmentId.HasValue)
                filtered = filtered.Where(o => o.EquipmentId == equipmentId.Value);

            var filteredList = filtered.ToList();
            var results = filteredList.Take(limit).Select(o => new
            {
                order_id        = o.OrderId,
                equipment_name  = o.Equipment?.Name,
                product_name    = BuildProductName(o.EquipmentProduct),
                process_name    = o.EquipmentProduct?.Sop?.Process?.Name,
                status          = o.Status.ToString(),
                quantity        = o.Quantity,
                started_at      = o.StartedAt?.ToString("yyyy-MM-dd HH:mm"),
                started_by_name = o.StartedByEmployee?.Name
            }).ToList();

            return JsonSerializer.Serialize(new { total = filteredList.Count, results });
        }
        catch (Exception ex) { core.Log.AddErrorLog($"[QueryOrdersAsync] {ex.Message}"); return """{"error":"tool_failed"}"""; }
    }

    // ── query_equipment_status ────────────────────────────────────────────────

    private static async Task<string> QueryEquipmentStatusAsync(DashboardCoreServices core, JsonObject args)
    {
        if (!core.Authorization.HasPermission(PermissionId.View))
            return """{"error":"no_permission"}""";
        try
        {
            var equipments = await core.Data.GetAllEquipmentAsync();
            var orders     = await core.Data.GetAllOrderProductionsAsync();

            var inProductionByEquipment = orders
                .Where(o => o.Status == Models.OrderProductionStatus.InProduction)
                .GroupBy(o => o.EquipmentId)
                .ToDictionary(g => g.Key, g => g.First());

            var results = equipments.Select(e =>
            {
                inProductionByEquipment.TryGetValue(e.Id, out var currentOrder);
                return new
                {
                    equipment_id     = e.Id,
                    code             = e.Code,
                    name             = e.Name,
                    type_name        = e.Type?.Name,
                    is_in_production = currentOrder != null,
                    current_order    = currentOrder == null ? null : (object)new
                    {
                        order_id     = currentOrder.OrderId,
                        product_name = BuildProductName(currentOrder.EquipmentProduct),
                        process_name = currentOrder.EquipmentProduct?.Sop?.Process?.Name,
                        started_at   = currentOrder.StartedAt?.ToString("yyyy-MM-dd HH:mm")
                    }
                };
            }).ToList();

            return JsonSerializer.Serialize(new { total = results.Count, results });
        }
        catch (Exception ex) { core.Log.AddErrorLog($"[QueryEquipmentStatusAsync] {ex.Message}"); return """{"error":"tool_failed"}"""; }
    }

    // ── query_tuning_status ───────────────────────────────────────────────────

    private static async Task<string> QueryTuningStatusAsync(DashboardCoreServices core, JsonObject args)
    {
        if (!core.Authorization.HasPermission(PermissionId.OperateTuning))
            return """{"error":"no_permission"}""";
        try
        {
            var tunings = await core.Data.GetAllInProgressProgramTuningAsync();

            var results = tunings.Select(t => new
            {
                equipment_name  = t.Equipment?.Name,
                product_name    = BuildProductName(t.EquipmentProduct),
                tuning_type     = t.TuningType.ToString(),
                started_at      = t.StartedAt.ToString("yyyy-MM-dd HH:mm"),
                started_by_name = t.StartedByEmployee?.Name
            }).ToList();

            return JsonSerializer.Serialize(new { total = results.Count, results });
        }
        catch (Exception ex) { core.Log.AddErrorLog($"[QueryTuningStatusAsync] {ex.Message}"); return """{"error":"tool_failed"}"""; }
    }

    // ── query_equipment_capabilities ──────────────────────────────────────────

    private static JsonObject BuildQueryEquipmentCapabilitiesSchema() => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "keyword": { "type": "string", "description": "件號、型號或製程名稱模糊比對" },
                "status":  { "type": "string", "enum": ["Feasible","All"], "description": "可生產狀態篩選：預設 Feasible（只回傳可生產），All 回傳全部" }
            },
            "required": []
        }
        """)!.AsObject();

    private static async Task<string> QueryEquipmentCapabilitiesAsync(DashboardCoreServices core, JsonObject args)
    {
        if (!core.Authorization.HasPermission(PermissionId.View))
            return """{"error":"no_permission"}""";
        try
        {
            var all = await core.Data.GetAllEquipmentProductsAsync();

            string? keyword = args["keyword"]?.GetValue<string>();
            bool showAll = args["status"]?.GetValue<string>()
                ?.Equals("All", StringComparison.OrdinalIgnoreCase) ?? false;

            var filtered = all.AsEnumerable();

            if (!showAll)
                filtered = filtered.Where(ep => ep.ProductionStatus == Models.TuningType.Feasible);

            if (!string.IsNullOrEmpty(keyword))
                filtered = filtered.Where(ep =>
                    (ep.Sop?.Product?.Part?.PartNo?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (ep.Sop?.Product?.Model?.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (ep.Sop?.Process?.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));

            var results = filtered.Select(ep => new
            {
                equipment_name    = ep.Equipment?.Name,
                equipment_code    = ep.Equipment?.Code,
                product_name      = BuildProductName(ep),
                process_name      = ep.Sop?.Process?.Name,
                production_status = ep.ProductionStatus.ToString(),
                seq_no            = ep.SeqNo
            }).ToList();

            return JsonSerializer.Serialize(new { total = results.Count, results });
        }
        catch (Exception ex) { core.Log.AddErrorLog($"[QueryEquipmentCapabilitiesAsync] {ex.Message}"); return """{"error":"tool_failed"}"""; }
    }

    // ── 共用輔助方法 ──────────────────────────────────────────────────────────

    private static JsonObject BuildEmptySchema() => JsonNode.Parse("""
        { "type": "object", "properties": {}, "required": [] }
        """)!.AsObject();

    private static string? BuildProductName(Models.EquipmentProduct? ep)
    {
        var part  = ep?.Sop?.Product?.Part?.PartNo;
        var model = ep?.Sop?.Product?.Model?.Name;
        if (part == null && model == null) return null;
        return $"{part}_{model}";
    }

    private static DateOnly? TryParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", out var d) ? d : null;
}
