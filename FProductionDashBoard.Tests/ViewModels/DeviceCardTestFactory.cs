using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;

namespace FProductionDashBoard.Tests.ViewModels
{
    // Inspection / Material 對話框的 ctor 吃具體 DeviceCardViewModel（非虛擬、不可 Moq），
    // 但僅讀取 Info.Name / CurrentUser.Name / CurrentProduct。此工廠建一個最小可用 card 供兩處共用。
    // 建構子的 LoadOrdersAsync / LoadProgramTuningStateAsync 為 fire-and-forget，不影響同步建構。
    internal static class DeviceCardTestFactory
    {
        public static DeviceCardViewModel Create(string deviceName = "M01", string userName = "Tester")
        {
            var core = new DashboardCoreServices(
                new LogService(),
                new Mock<IDataService>().Object,
                new AuthorizationService(),
                new Mock<ICardReaderService>().Object,
                new Mock<IWarehouseService>().Object);
            var info           = new DeviceInfo { DeviceID = "D1", Name = deviceName };
            var user           = new UserInfo { UserId = "u1", Name = userName };
            var lists          = new ListsFromSql();
            var hardwareConfig = new Mock<IConfigService<HardwareConfigDto>>().Object;
            return new DeviceCardViewModel(core, new Mock<IDialogService>().Object, info, user, lists, hardwareConfig);
        }
    }
}
