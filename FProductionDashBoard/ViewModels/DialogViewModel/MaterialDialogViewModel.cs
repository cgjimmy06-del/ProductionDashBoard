using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using FProductionDashBoard.UiModels;
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
        public List<MaterialInfo> Selections { get; set; } = new();
    }
    internal partial class MaterialDialogViewModel : DialogBaseViewModel<MaterialResult>
    {
        [ObservableProperty]
        private string? currentDevice;
        [ObservableProperty]
        private bool isCountMode = false; // 選擇模式或計數模式

        public ObservableCollection<MaterialInfo> Materials { get; } = new();
        public IEnumerable<MaterialInfo> SelectedMaterials => Materials.Where(m => m.IsSelected);

        public MaterialDialogViewModel(string dialogstring, DeviceCardViewModel getinfo, 
            List<MaterialInfo> sqlmateriallist) : base(dialogstring)
        {
            CurrentDevice = $"{Properties.Resources.ComStrDevice}: {getinfo.Info.Name}";
            Materials = new ObservableCollection<MaterialInfo>(sqlmateriallist);

            ConfirmCommand = new RelayCommand(() => OnConfirm());
            CancelCommand = new RelayCommand(() => OnCancel());
        }

        [RelayCommand]
        private void MaterialButtonClick(MaterialInfo selectedMaterial)
        {
            if (!IsCountMode)
            {
                selectedMaterial.IsSelected = !selectedMaterial.IsSelected;
                selectedMaterial.SelectedCount = selectedMaterial.IsSelected ? 1 : 0; ;
            }
            else
            {
                selectedMaterial.IsSelected = true;
                selectedMaterial.SelectedCount++;
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

            Result = new MaterialResult() { Selections = SelectedMaterials.ToList() };

            base.OnConfirm();
        }
    }
}
