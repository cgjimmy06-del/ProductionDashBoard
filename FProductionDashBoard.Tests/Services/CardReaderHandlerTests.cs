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
                mockCardReader.Object);

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
    }
}
