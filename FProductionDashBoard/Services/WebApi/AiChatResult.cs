namespace FProductionDashBoard.Services.WebApi
{
    /// <summary>AI 對話單次往返結果：回覆文字與本次消耗的 tokens（tool-calling 多回合已加總）</summary>
    public record AiChatResult(string Reply, int InputTokens, int OutputTokens);
}
