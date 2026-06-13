using FProductionDashBoard.UiModels;

namespace FProductionDashBoard.Services.WebApi
{
    public interface IAiChatService
    {
        bool IsConfigured { get; }

        Task<string> SendAsync(
            string model,
            IEnumerable<ChatMessage> history,
            string userMessage);
    }
}
