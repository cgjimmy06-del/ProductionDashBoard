using System.Text.Json.Nodes;

namespace FProductionDashBoard.Services.AiTools;

public record AiToolDefinition(
    string Name,
    string Description,
    JsonObject Parameters,
    Func<JsonObject, Task<string>> Handler
);
