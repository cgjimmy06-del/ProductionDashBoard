using DeviceDrivers.Abb;
using FProductionDashBoard.Services;
using System;

namespace FProductionDashBoard.ViewModels
{
    public class HardwareViewModel
    {
        public CardReaderSettingViewModel CardReader { get; }
        public AbbRobotSettingViewModel AbbRobot { get; }

        public HardwareViewModel(MultiCardReaderService multi, Func<IAbbRobotClient> abbClientFactory)
        {
            CardReader = new CardReaderSettingViewModel(multi);
            AbbRobot = new AbbRobotSettingViewModel(abbClientFactory);
        }
    }
}
