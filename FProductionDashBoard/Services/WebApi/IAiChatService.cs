using FProductionDashBoard.UiModels;

namespace FProductionDashBoard.Services.WebApi
{
    public interface IAiChatService
    {
        bool IsConfigured { get; }
        string[] AvailableModels { get; }

        Task<string> SendAsync(
            string model,
            IEnumerable<ChatMessage> history,
            string userMessage);
    }
}
