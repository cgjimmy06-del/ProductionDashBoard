using FProductionDashBoard.Services.AiTools;
using FProductionDashBoard.UiModels;

namespace FProductionDashBoard.Services.WebApi
{
    public interface IAiChatService
    {
        bool IsConfigured { get; }
        string[] AvailableModels { get; }

        Task<AiChatResult> SendAsync(
            string model,
            IEnumerable<ChatMessage> history,
            string userMessage,
            IReadOnlyList<AiToolDefinition>? tools = null,
            string? systemPrompt = null,
            CancellationToken ct = default);
    }
}
