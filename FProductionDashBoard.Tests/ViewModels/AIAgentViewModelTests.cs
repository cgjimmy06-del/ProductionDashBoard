using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.AiTools;
using FProductionDashBoard.Services.WebApi;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class AIAgentViewModelTests
    {
        // 驗證 SendAsync 的 session 快照：await 期間切換 session 時，回覆與 tokens 仍記入發話當下的 session

        private readonly Mock<IAiChatService> _chat = new();
        private readonly Mock<IDataService> _data = new();

        private AIAgentViewModel CreateVm(TaskCompletionSource<AiChatResult> tcs)
        {
            _chat.SetupGet(c => c.IsConfigured).Returns(true);
            _chat.SetupGet(c => c.AvailableModels).Returns(new[] { "test-model" });
            _chat.Setup(c => c.SendAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<AiToolDefinition>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var log = new LogService();
            var auth = new AuthorizationService();
            var cardReader = new Mock<ICardReaderService>().Object;
            var core = new DashboardCoreServices(log, _data.Object, auth, cardReader, new Mock<IWarehouseService>().Object);
            return new AIAgentViewModel(_chat.Object, core, new AiAgentToolService(core));
        }

        [Fact]
        public async Task SendAsync_Completes_UpdatesCurrentSessionAndMirror()
        {
            var tcs = new TaskCompletionSource<AiChatResult>();
            var vm = CreateVm(tcs);
            var session = vm.CurrentSession;

            vm.InputText = "hello";
            var sendTask = ((IAsyncRelayCommand)vm.SendCommand).ExecuteAsync(null);
            tcs.SetResult(new AiChatResult("回覆", 120, 45));
            await sendTask;

            Assert.Contains(session.Messages, m => m.Sender == ChatSender.Ai && m.Content == "回覆");
            Assert.DoesNotContain(session.Messages, m => m.IsTyping);
            Assert.Equal(120, session.InputTokens);
            Assert.Equal(45, session.OutputTokens);
            Assert.Equal(120, vm.SessionInputTokens);
            Assert.Equal(45, vm.SessionOutputTokens);
        }

        [Fact]
        public async Task SendAsync_SessionSwitchedDuringAwait_ReplyAndTokensGoToOriginalSession()
        {
            var tcs = new TaskCompletionSource<AiChatResult>();
            var vm = CreateVm(tcs);
            var original = vm.CurrentSession;

            vm.InputText = "hello";
            var sendTask = ((IAsyncRelayCommand)vm.SendCommand).ExecuteAsync(null);

            vm.NewSessionCommand.Execute(null);   // 等待回覆期間切換到新 session
            var switched = vm.CurrentSession;
            Assert.NotSame(original, switched);

            tcs.SetResult(new AiChatResult("回覆", 120, 45));
            await sendTask;

            // 回覆與 tokens 落在原 session，且無殘留的思考中訊息
            Assert.Contains(original.Messages, m => m.Sender == ChatSender.Ai && m.Content == "回覆");
            Assert.DoesNotContain(original.Messages, m => m.IsTyping);
            Assert.Equal(120, original.InputTokens);
            Assert.Equal(45, original.OutputTokens);

            // 切換後的 session 不受影響，鏡像屬性維持 0
            Assert.DoesNotContain(switched.Messages, m => m.Content == "回覆");
            Assert.Equal(0, switched.InputTokens);
            Assert.Equal(0, vm.SessionInputTokens);
            Assert.Equal(0, vm.SessionOutputTokens);
        }
    }
}
