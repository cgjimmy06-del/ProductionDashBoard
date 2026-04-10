using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.UiModels
{
    public partial class MaterialInfo : ObservableObject
    {
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = ""; // 顯示用

        [ObservableProperty]
        private bool isSelected = false;
        [ObservableProperty]
        private int selectedCount = 0;


    }
}
