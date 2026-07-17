using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.AiTools;
using FProductionDashBoard.UiModels;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FProductionDashBoard.Services.WebApi
{
    public class OpenAiChatService : IAiChatService
    {
        private readonly HttpClient _httpClient;
        private readonly AiApiOptions _options;
        private readonly IConfigService<SystemConfigDto> _systemConfig;

        private const int MaxToolRounds = 5;

        public OpenAiChatService(HttpClient httpClient, IOptions<AiApiOptions> options,
            IConfigService<SystemConfigDto> systemConfig)
        {
            _httpClient   = httpClient;
            _options      = options.Value;
            _systemConfig = systemConfig;
        }

        public bool IsConfigured =>
            !string.IsNullOrEmpty(_options.BaseUrl) &&
            !string.IsNullOrEmpty(_systemConfig.Current.AiApiKey) &&
            _options.AvailableModels.Length > 0;

        public string[] AvailableModels => _options.AvailableModels;

        public async Task<AiChatResult> SendAsync(
            string model,
            IEnumerable<ChatMessage> history,
            string userMessage,
            IReadOnlyList<AiToolDefinition>? tools = null,
            string? systemPrompt = null,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("[SendAsync] AI 未設定：請於系統設定填入 AiApi:BaseUrl / ApiKey");

            var inputItems = new JsonArray();
            foreach (var m in history.Where(m => !m.IsTyping))
            {
                inputItems.Add(JsonNode.Parse(JsonSerializer.Serialize(new
                {
                    role    = m.Sender == ChatSender.User ? "user" : "assistant",
                    content = m.Content
                })));
            }
            inputItems.Add(JsonNode.Parse(JsonSerializer.Serialize(new { role = "user", content = userMessage })));

            int inputTokens = 0, outputTokens = 0;

            for (int round = 0; round < MaxToolRounds; round++)
            {
                var bodyObj = new JsonObject
                {
                    ["model"] = model,
                    ["input"] = inputItems.DeepClone()
                };

                if (!string.IsNullOrEmpty(systemPrompt))
                    bodyObj["instructions"] = systemPrompt;

                if (tools is { Count: > 0 })
                    bodyObj["tools"] = BuildToolsArray(tools);

                var bodyJson = bodyObj.ToJsonString();
                var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("api-key", _systemConfig.Current.AiApiKey);

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
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
                var root = JsonNode.Parse(json)!;
                var output = root["output"]!.AsArray();

                // 逐回合累加 usage（欄位缺失視為 0，不拋錯）
                inputTokens  += root["usage"]?["input_tokens"]?.GetValue<int>() ?? 0;
                outputTokens += root["usage"]?["output_tokens"]?.GetValue<int>() ?? 0;

                var funcCallNode = output.FirstOrDefault(n => n?["type"]?.GetValue<string>() == "function_call");
                if (funcCallNode == null)
                {
                    var msgNode = output.FirstOrDefault(n => n?["type"]?.GetValue<string>() == "message");
                    var text = msgNode?["content"]?[0]?["text"]?.GetValue<string>() ?? "";
                    return new AiChatResult(text, inputTokens, outputTokens);
                }

                // 執行工具
                var callId   = funcCallNode["call_id"]!.GetValue<string>();
                var funcName = funcCallNode["name"]!.GetValue<string>();
                var argsNode = JsonNode.Parse(funcCallNode["arguments"]!.GetValue<string>()) as JsonObject
                               ?? new JsonObject();

                var tool = tools?.FirstOrDefault(t => t.Name == funcName);
                string toolResult;
                if (tool != null)
                {
                    try   { toolResult = await tool.Handler(argsNode); }
                    catch { toolResult = """{"error":"tool_failed"}"""; }
                }
                else
                    toolResult = """{"error":"unknown_tool"}""";

                inputItems.Add(funcCallNode.DeepClone());
                inputItems.Add(JsonNode.Parse(JsonSerializer.Serialize(new
                {
                    type     = "function_call_output",
                    call_id  = callId,
                    output   = toolResult
                })));
            }

            return new AiChatResult(Properties.Resources.AiMaxRoundsExceeded, inputTokens, outputTokens);
        }

        private static JsonArray BuildToolsArray(IReadOnlyList<AiToolDefinition> tools)
        {
            var arr = new JsonArray();
            foreach (var t in tools)
            {
                arr.Add(new JsonObject
                {
                    ["type"]        = "function",
                    ["name"]        = t.Name,
                    ["description"] = t.Description,
                    ["parameters"]  = t.Parameters.DeepClone()
                });
            }
            return arr;
        }
    }
}
