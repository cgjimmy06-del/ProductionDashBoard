using DeviceDrivers.Abb;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using System;

namespace FProductionDashBoard.ViewModels
{
    public class HardwareViewModel
    {
        public CardReaderSettingViewModel CardReader { get; }
        public AbbRobotSettingViewModel AbbRobot { get; }

        public HardwareViewModel(MultiCardReaderService multi, Func<IAbbRobotClient> abbClientFactory,
            IConfigService<HardwareConfigDto> hardwareConfig)
        {
            CardReader = new CardReaderSettingViewModel(multi, hardwareConfig);
            AbbRobot = new AbbRobotSettingViewModel(abbClientFactory);
        }
    }
}
