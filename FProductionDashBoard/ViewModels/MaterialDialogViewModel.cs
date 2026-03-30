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

namespace FProductionDashBoard
{
    public class MaterialResult
    {
        public required MaterialInfo[] Selections;
    }
    internal partial class MaterialDialogViewModel : DialogBaseViewModel<MaterialResult>
    {
        [ObservableProperty]
        private string? currentDevice;
        [ObservableProperty]
        private bool isCountMode = false; // 選擇模式或計數模式
        
        public ObservableCollection<MaterialInfo> Materials { get; } = new () {
            new MaterialInfo { Code = "AB001", Name = "A16" }, 
            new MaterialInfo { Code = "AB002", Name = "305EA" },
            new MaterialInfo { Code = "AB003", Name = "307EA" },
            new MaterialInfo { Code = "AB004", Name = "PZ533" },
            new MaterialInfo { Code = "AB005", Name = "PZ220" },
            new MaterialInfo { Code = "AB006", Name = "340" },
            new MaterialInfo { Code = "AB007", Name = "KAX" },
            new MaterialInfo { Code = "AB008", Name = "180#" },
            new MaterialInfo { Code = "AB009", Name = "60#" },
            new MaterialInfo { Code = "AB0010", Name = "#100" },
            new MaterialInfo { Code = "AB0011", Name = "PX220" },
            new MaterialInfo { Code = "AB0012", Name = "JA539 400" },
            new MaterialInfo { Code = "AB0013", Name = "JA539 180" }
        };
        public IEnumerable<MaterialInfo> SelectedMaterials => Materials.Where(m => m.IsSelected);

        public MaterialDialogViewModel(string dialogstring, DeviceCardViewModel getinfo) : base(dialogstring)
        {
            CurrentDevice = getinfo.Info.Name;

            ConfirmCommand = new RelayCommand(() => OnConfirm());
            CancelCommand = new RelayCommand(() => OnCancel());
        }

        [RelayCommand]
        private void MaterialButtonClick(MaterialInfo selectedmaterial)
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
            System.Diagnostics.Debug.WriteLine($"Button clicked: {selectedmaterial.Code}-{selectedmaterial.SelectedCount}");
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
            { DialogErrorString = "請選擇物料!"; return; }

            Result = new MaterialResult() { Selections = SelectedMaterials.ToArray() };

            IsConfirmed = true;
            OnRequestClose();
        }
    }
}
