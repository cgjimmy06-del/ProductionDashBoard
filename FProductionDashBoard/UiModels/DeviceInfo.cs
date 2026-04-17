using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static Dapper.SqlMapper;

namespace FProductionDashBoard.UiModels
{
    public partial class DeviceInfo : ObservableObject
    {
        public int Id { get; set; }
        public required string DeviceID { get; set; }
        public required string Name { get; set; }
        public string IP { get; set; } = "none";
        public int Port { get; set; } = 0;
        public int? TypeId { get; set; }
        public string? Factory { get; set; }
        public string? Building { get; set; }
        public string? Floor { get; set; }
        public string? Description { get; set; }
        [ObservableProperty]
        private int status;
    }
}
