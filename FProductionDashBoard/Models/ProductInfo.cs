using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard
{
    public partial class ProductInfo : ObservableObject 
    {
        public string Name => $"{ModelCode}_{TypeCode}";

        [ObservableProperty]
        private string modelCode = string.Empty;
        [ObservableProperty]
        private string typeCode = string.Empty;


    }
}
