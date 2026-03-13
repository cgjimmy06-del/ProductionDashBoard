using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard
{
    public class WorkerInfo
    {
        public required string ID { get; set; }   // 主鍵
        public required string Name { get; set; }
        public string? Email { get; set; } = null;
        public string Password { get; set; } = "";
        public int Permission { get; set; } = 0;


        //public ICollection<DeviceInfo> Devices { get; set; } = new List<DeviceInfo>();
    }
}
