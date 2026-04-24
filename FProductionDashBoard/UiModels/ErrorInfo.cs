using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.UiModels
{
    public class ErrorInfo
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string? Message { get; set; }
        public int? TypeId { get; set; }
    }
}
