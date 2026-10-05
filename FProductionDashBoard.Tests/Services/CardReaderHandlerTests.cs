using FProductionDashBoard.Dtos;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.WebApi;
using FProductionDashBoard.UiModels;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class CardReaderHandlerTests
    {
        private static (CardReaderHandler handler,
            Mock<ICardReaderService> mockCardReader,
            Mock<IErpApiService> mockErp,
            ListsFromSql lists) CreateHandler()
        {
            var mockCardReader = new Mock<ICardReaderService>();
            var mockData = new Mock<IDataService>();
            var mockRepo = new Mock<IRolePermissionRepository>();
            var core = new DashboardCoreServices(
                new LogService(),
                mockData.Object,
                new AuthorizationService(),
                mockCardReader.Object,
                new Mock<IWarehouseService>().Object);

            var mockErp = new Mock<IErpApiService>();
            var mockDialog = new Mock<IDialogService>();
            var lists = new ListsFromSql();

            var handler = new CardReaderHandler(core, mockErp.Object, mockDialog.Object, lists);

            return (handler, mockCardReader, mockErp, lists);
        }

        private static void RaiseCardRead(Mock<ICardReaderService> mock, string cardId)
            => mock.Raise(r => r.CardRead += null, new object(), new CardReadEventArgs(cardId, "COM1"));

        [Fact]
        public async Task KnownCard_DoesNotQueryErp()
        {
            var (handler, mockCardReader, mockErp, lists) = CreateHandler();
            lists.UsersList = [new UserInfo { UserId = "A001", Name = "Alice", CardId = "KNOWN" }];
            handler.Attach();

            RaiseCardRead(mockCardReader, "KNOWN");
            await Task.Delay(200);

            mockErp.Verify(e => e.GetEmpInfoByCardAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UnknownCard_ErpReturnsNull_LogsAndDoesNotAddEmployee()
        {
            var (handler, mockCardReader, mockErp, _) = CreateHandler();
            mockErp.Setup(e => e.GetEmpInfoByCardAsync("UNKNOWN")).ReturnsAsync((EmpInfoDto?)null);
            handler.Attach();

            RaiseCardRead(mockCardReader, "UNKNOWN");
            await Task.Delay(200);

            mockErp.Verify(e => e.GetEmpInfoByCardAsync("UNKNOWN"), Times.Once);
        }

        [Fact]
        public async Task AfterDetach_CardRead_HandlerNotInvoked()
        {
            var (handler, mockCardReader, mockErp, _) = CreateHandler();
            mockErp.Setup(e => e.GetEmpInfoByCardAsync(It.IsAny<string>())).ReturnsAsync((EmpInfoDto?)null);
            handler.Attach();
            handler.Detach();

            RaiseCardRead(mockCardReader, "UNKNOWN");
            await Task.Delay(200);

            mockErp.Verify(e => e.GetEmpInfoByCardAsync(It.IsAny<string>()), Times.Never);
        }

        // 重入契約：OnCardRead 以 SemaphoreSlim.WaitAsync(0) 防重入。第一次刷卡持鎖期間
        // 再次刷卡，第二次必須被立即擋下（不進入業務邏輯、不重複查 ERP）。
        // 用未完成的 TCS 卡住第一次於 ERP 查詢處持鎖，藉此觀察第二次是否被擋。
        [Fact]
        public async Task ConcurrentCardRead_SecondIsRejectedByReentrancyLock()
        {
            var (handler, mockCardReader, mockErp, _) = CreateHandler();
            var entered = new TaskCompletionSource();
            var gate = new TaskCompletionSource<EmpInfoDto?>();
            // 第一次刷卡：進入 ERP 查詢時發出訊號，並回未完成 Task 使其持鎖停在 await 不放
            mockErp.Setup(e => e.GetEmpInfoByCardAsync("UNKNOWN"))
                .Returns(() => { entered.TrySetResult(); return gate.Task; });
            handler.Attach();

            // 第一次觸發：等到確實走進 GetEmpInfoByCardAsync（已持鎖），不靠固定時間猜測
            RaiseCardRead(mockCardReader, "UNKNOWN");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // 第二次觸發（持鎖期間）：WaitAsync(0) 同步回 false → 直接 return，可立即驗證
            RaiseCardRead(mockCardReader, "UNKNOWN");

            // ERP 只應被呼叫一次（第二次被重入鎖擋下）
            mockErp.Verify(e => e.GetEmpInfoByCardAsync("UNKNOWN"), Times.Once);

            // 釋放第一次讓其正常完成（清理，避免懸置 Task）；完成後仍只有一次 ERP 查詢
            gate.SetResult(null);
            await Task.Delay(50);
            mockErp.Verify(e => e.GetEmpInfoByCardAsync("UNKNOWN"), Times.Once);
        }
    }
}
