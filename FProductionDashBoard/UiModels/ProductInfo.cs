using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.UiModels
{
    public partial class ProductInfo : ObservableObject
    {
        public int? ProductId { get; set; }
        public string ProductName = string.Empty;

    }
}
