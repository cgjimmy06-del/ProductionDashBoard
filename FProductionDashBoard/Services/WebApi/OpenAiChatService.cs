using FProductionDashBoard.UiModels;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace FProductionDashBoard.Services.WebApi
{
    public class OpenAiChatService : IAiChatService
    {
        private readonly HttpClient _httpClient;
        private readonly AiApiOptions _options;

        public OpenAiChatService(HttpClient httpClient, IOptions<AiApiOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public bool IsConfigured =>
            !string.IsNullOrEmpty(_options.ApiKey) && !string.IsNullOrEmpty(_options.BaseUrl);

        public async Task<string> SendAsync(string model, IEnumerable<ChatMessage> history, string userMessage)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("[SendAsync] AI 未設定：請填入 appsettings.json AiApi:BaseUrl / ApiKey");

            var messages = history
                .Where(m => !m.IsTyping)
                .Select(m => new
                {
                    role    = m.Sender == ChatSender.User ? "user" : "assistant",
                    content = m.Content
                })
                .Append(new { role = "user", content = userMessage })
                .ToArray<object>();

            var body = JsonSerializer.Serialize(new { model, input = messages });
            var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("api-key", _options.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request);
            }
            catch (TaskCanceledException)
            {
                throw new InvalidOperationException("[SendAsync] 請求逾時，請重試");
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"[SendAsync] 連線失敗：{ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"[SendAsync] OpenAI API 回應失敗：{(int)response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("output")[0]
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "";
        }
    }
}
