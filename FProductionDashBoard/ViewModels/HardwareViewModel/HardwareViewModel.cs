using DeviceDrivers.Abb;
using DeviceDrivers.Modbus;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using System;

namespace FProductionDashBoard.ViewModels
{
    public class HardwareViewModel
    {
        public CardReaderSettingViewModel CardReader { get; }
        public AbbRobotSettingViewModel AbbRobot { get; }
        public ModbusTcpSettingViewModel ModbusTcp { get; }

        public HardwareViewModel(MultiCardReaderService multi, Func<IAbbRobotClient> abbClientFactory,
            IConfigService<HardwareConfigDto> hardwareConfig,
            Func<string, int, byte, IModbusClient> modbusFactory)
        {
            CardReader = new CardReaderSettingViewModel(multi, hardwareConfig);
            AbbRobot   = new AbbRobotSettingViewModel(abbClientFactory);
            ModbusTcp  = new ModbusTcpSettingViewModel(modbusFactory);
        }
    }
}
