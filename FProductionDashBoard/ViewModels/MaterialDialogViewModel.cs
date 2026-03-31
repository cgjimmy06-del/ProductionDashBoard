using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace FProductionDashBoard.ViewModels
{
    public class MaterialResult
    {
        public required Models.MaterialInfo[] Selections;
    }
    internal partial class MaterialDialogViewModel : DialogBaseViewModel<MaterialResult>
    {
        [ObservableProperty]
        private string? currentDevice;
        [ObservableProperty]
        private bool isCountMode = false; // 選擇模式或計數模式
        
        public ObservableCollection<Models.MaterialInfo> Materials { get; } = new () {
            new Models.MaterialInfo { Code = "AB001", Name = "A16" }, 
            new Models.MaterialInfo { Code = "AB002", Name = "305EA" },
            new Models.MaterialInfo { Code = "AB003", Name = "307EA" },
            new Models.MaterialInfo { Code = "AB004", Name = "PZ533" },
            new Models.MaterialInfo { Code = "AB005", Name = "PZ220" },
            new Models.MaterialInfo { Code = "AB006", Name = "340" },
            new Models.MaterialInfo { Code = "AB007", Name = "KAX" },
            new Models.MaterialInfo { Code = "AB008", Name = "180#" },
            new Models.MaterialInfo { Code = "AB009", Name = "60#" },
            new Models.MaterialInfo { Code = "AB0010", Name = "#100" },
            new Models.MaterialInfo { Code = "AB0011", Name = "PX220" },
            new Models.MaterialInfo { Code = "AB0012", Name = "JA539 400" },
            new Models.MaterialInfo { Code = "AB0013", Name = "JA539 180" }
        };
        public IEnumerable<Models.MaterialInfo> SelectedMaterials => Materials.Where(m => m.IsSelected);

        public MaterialDialogViewModel(string dialogstring, DeviceCardViewModel getinfo) : base(dialogstring)
        {
            CurrentDevice = getinfo.Info.Name;

            ConfirmCommand = new RelayCommand(() => OnConfirm());
            CancelCommand = new RelayCommand(() => OnCancel());
        }

        [RelayCommand]
        private void MaterialButtonClick(Models.MaterialInfo selectedmaterial)
        {
            if (!IsCountMode)
            {
                selectedmaterial.IsSelected = !selectedmaterial.IsSelected;
                selectedmaterial.SelectedCount = selectedmaterial.IsSelected ? 1 : 0; ;
            }
            else
            {
                selectedmaterial.IsSelected = true;
                selectedmaterial.SelectedCount++;
            }
            DialogErrorString = "";
            //OnPropertyChanged(nameof(SelectedMaterials));
        }

        [RelayCommand]
        private void ToggleMode() { IsCountMode = !IsCountMode; ClearSelection(); }
        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var m in Materials) { m.IsSelected = false; m.SelectedCount = 0; }
        }
        protected override void OnConfirm()
        {
            if (!SelectedMaterials.Any())
            { DialogErrorString = Properties.Resources.MaterialNonSelectionError; return; }

            Result = new MaterialResult() { Selections = SelectedMaterials.ToArray() };

            IsConfirmed = true;
            OnRequestClose();
        }
    }
}
