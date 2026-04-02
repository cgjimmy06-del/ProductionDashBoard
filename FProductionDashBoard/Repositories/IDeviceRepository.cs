using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using FProductionDashBoard.Models;

namespace FProductionDashBoard.Repositories
{
    public class DeviceDto
    {
        public string? DeviceID { get; set; }
        public string? Name { get; set; }
        public string? IP { get; set; }
    }

    public interface IDeviceRepository : IRepository<DeviceInfo, MesDbContext>
    {

        public Task<IEnumerable<DeviceDto>> GetDevicesAllDapperAsync();

    }


}
