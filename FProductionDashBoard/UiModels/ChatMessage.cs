namespace FProductionDashBoard.UiModels
{
    public enum ChatSender { Ai, User }

    public class ChatMessage
    {
        public ChatSender Sender  { get; init; }
        public string Content     { get; init; } = "";
        public DateTime Time      { get; init; } = DateTime.Now;
        public bool IsTyping      { get; init; }
    }
}
