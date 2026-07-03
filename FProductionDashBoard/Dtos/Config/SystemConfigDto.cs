namespace FProductionDashBoard.Dtos
{
    public class SystemConfigDto
    {
        public int  BusinessHour           { get; set; } = 8;
        public int  BusinessMinute         { get; set; } = 0;
        public bool CardRefreshEnabled     { get; set; } = true;
        public int  CardRefreshIntervalSec { get; set; } = 30;
        public bool MissedCheckEnabled     { get; set; } = true;
        public int  MissedCheckIntervalSec { get; set; } = 300;
        public bool IdleLogoutEnabled      { get; set; } = true;
        public int  IdleLogoutIntervalSec  { get; set; } = 600;
        public bool LogSaveToFile          { get; set; } = false;
        public int  LogDaysToKeep          { get; set; } = 7;
        public string AiApiKey             { get; set; } = "";
    }
}
