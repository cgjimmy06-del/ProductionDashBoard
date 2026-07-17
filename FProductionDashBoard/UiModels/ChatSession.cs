using System.Collections.ObjectModel;

namespace FProductionDashBoard.UiModels
{
    public class ChatSession
    {
        public Guid     Id        { get; init; } = Guid.NewGuid();
        public string   Title     { get; set; }  = "新對話";
        public string   Category  { get; set; }  = "";
        public string   ModelUsed { get; set; }  = "";
        public DateTime LastTime  { get; set; }  = DateTime.Now;
        public bool     IsStarred { get; set; }
        /// <summary>本對話累計輸入 tokens（UI 顯示走 VM 鏡像屬性）</summary>
        public int      InputTokens  { get; set; }
        /// <summary>本對話累計輸出 tokens（UI 顯示走 VM 鏡像屬性）</summary>
        public int      OutputTokens { get; set; }
        public ObservableCollection<ChatMessage> Messages { get; } = new();
    }
}
