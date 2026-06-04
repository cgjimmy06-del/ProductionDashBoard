namespace FProductionDashBoard.Dtos
{
    public class HardwareConfigDto
    {
        public string ReaderPort       { get; set; } = "COM3";
        public int    ReaderBaud       { get; set; } = 115200;
        public string AbbSeqNoTask     { get; set; } = "T_ROB1";
        public string AbbSeqNoModule   { get; set; } = "MES";
        public string AbbSeqNoVariable { get; set; } = "MES_project";
        public bool   DeviceReconnectEnabled     { get; set; } = true;
        public int    DeviceReconnectIntervalSec { get; set; } = 10;
    }
}
