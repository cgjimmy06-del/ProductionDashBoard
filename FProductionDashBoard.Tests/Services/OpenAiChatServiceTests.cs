using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.AiTools;
using FProductionDashBoard.Services.WebApi;
using FProductionDashBoard.UiModels;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class OpenAiChatServiceTests
    {
        /// <summary>依序回傳預先排入的 JSON 回應（每次呼叫取出一筆）</summary>
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Queue<string> _responses;
            public FakeHttpMessageHandler(params string[] responses) => _responses = new(responses);

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json")
                });
        }

        private static OpenAiChatService CreateService(params string[] responses)
        {
            var httpClient = new HttpClient(new FakeHttpMessageHandler(responses));
            var options = Options.Create(new AiApiOptions
            {
                BaseUrl = "https://fake.local/openai/responses",
                AvailableModels = ["test-model"]
            });
            var configMock = new Mock<IConfigService<SystemConfigDto>>();
            configMock.SetupGet(c => c.Current).Returns(new SystemConfigDto { AiApiKey = "fake-key" });
            return new OpenAiChatService(httpClient, options, configMock.Object);
        }

        private static string MessageResponse(string text, int? inputTokens = null, int? outputTokens = null)
        {
            var usage = inputTokens.HasValue
                ? $@",""usage"":{{""input_tokens"":{inputTokens},""output_tokens"":{outputTokens},""total_tokens"":{inputTokens + outputTokens}}}"
                : "";
            return $@"{{""output"":[{{""type"":""message"",""content"":[{{""text"":""{text}""}}]}}]{usage}}}";
        }

        private static string FunctionCallResponse(string toolName, int inputTokens, int outputTokens)
            => $@"{{""output"":[{{""type"":""function_call"",""call_id"":""call_1"",""name"":""{toolName}"",""arguments"":""{{}}""}}]," +
               $@"""usage"":{{""input_tokens"":{inputTokens},""output_tokens"":{outputTokens},""total_tokens"":{inputTokens + outputTokens}}}}}";

        [Fact]
        public async Task SendAsync_SingleRound_ReturnsReplyWithUsage()
        {
            var service = CreateService(MessageResponse("你好", inputTokens: 120, outputTokens: 45));

            var result = await service.SendAsync("test-model", Array.Empty<ChatMessage>(), "hi");

            Assert.Equal("你好", result.Reply);
            Assert.Equal(120, result.InputTokens);
            Assert.Equal(45, result.OutputTokens);
        }

        [Fact]
        public async Task SendAsync_ToolCallRound_SumsUsageAcrossRounds()
        {
            var service = CreateService(
                FunctionCallResponse("query_test", inputTokens: 100, outputTokens: 20),
                MessageResponse("查詢完成", inputTokens: 250, outputTokens: 35));
            var tools = new[]
            {
                new AiToolDefinition("query_test", "測試工具", new JsonObject(),
                    _ => Task.FromResult(@"{""ok"":true}"))
            };

            var result = await service.SendAsync("test-model", Array.Empty<ChatMessage>(), "hi", tools);

            Assert.Equal("查詢完成", result.Reply);
            Assert.Equal(350, result.InputTokens);
            Assert.Equal(55, result.OutputTokens);
        }

        [Fact]
        public async Task SendAsync_UsageMissing_ReturnsZeroTokensWithoutThrow()
        {
            var service = CreateService(MessageResponse("無用量欄位"));

            var result = await service.SendAsync("test-model", Array.Empty<ChatMessage>(), "hi");

            Assert.Equal("無用量欄位", result.Reply);
            Assert.Equal(0, result.InputTokens);
            Assert.Equal(0, result.OutputTokens);
        }
    }
}
