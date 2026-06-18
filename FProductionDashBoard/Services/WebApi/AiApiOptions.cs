namespace FProductionDashBoard.Services.WebApi
{
    public class AiApiOptions
    {
        public string   BaseUrl         { get; set; } = "";
        public string[] AvailableModels { get; set; } = ["gpt-5.4-mini", "gpt-5.4"];
    }
}
