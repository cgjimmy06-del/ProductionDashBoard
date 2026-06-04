namespace FProductionDashBoard.Dtos
{
    public class SystemConfigDto
    {
        public int  BusinessHour           { get; set; } = 8;
        public int  BusinessMinute         { get; set; } = 0;
        public bool SyncEnabled            { get; set; } = true;
        public int  SyncIntervalSec        { get; set; } = 60;
        public bool MissedCheckEnabled     { get; set; } = true;
        public int  MissedCheckIntervalSec { get; set; } = 300;
        public bool IdleLogoutEnabled      { get; set; } = true;
        public int  IdleLogoutIntervalSec  { get; set; } = 600;
        public bool LogSaveToFile          { get; set; } = false;
        public int  LogDaysToKeep          { get; set; } = 7;
    }
}
