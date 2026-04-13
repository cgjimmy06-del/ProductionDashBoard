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
        public int Id { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Brand { get; set; }
        public string? Specification { get; set; }
        public int? TypeId { get; set; }
        public string? Description { get; set; }
        public int MinimumStock { get; set; } = 0;
        public int QuantityInStock { get; set; } = 0;

        public string NameDisplay => Description ?? Name;
        [ObservableProperty]
        private bool isSelected = false;
        [ObservableProperty]
        private int selectedCount = 0;

    }
}
