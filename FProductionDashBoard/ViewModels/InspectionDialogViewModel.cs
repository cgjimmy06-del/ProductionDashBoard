using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard
{
    public enum InspectionRadioCheck
    {
        Success,
        Failure
    }
    public class InspectionResult
    {
        public required bool IsNormal { get; set; }
        public string Description { get; set; } = "";
    }

    public partial class InspectionDialogViewModel : DialogBaseViewModel<InspectionResult>
    {
        [ObservableProperty]
        public string? inspectionMode;
        [ObservableProperty]
        public string? currentDevice;
        [ObservableProperty]
        public string? currentProduct;
        [ObservableProperty]
        public string? currentUser;
        public ObservableCollection<string> ErrorString { get; } =
        new ObservableCollection<string> { "error1", "error2", "error3", "other" };
        [ObservableProperty]
        private InspectionRadioCheck inspectionCheck = InspectionRadioCheck.Success;
        [ObservableProperty]
        private string selectionDescription;
        [ObservableProperty]
        private string otherDescription = "";
        public bool IsOtherSelected => SelectionDescription == ErrorString.LastOrDefault();
        partial void OnSelectionDescriptionChanged(string value)
        { OnPropertyChanged(nameof(IsOtherSelected)); }

        public InspectionDialogViewModel(string mode, DeviceCardViewModel getinfo)
        {
            inspectionMode = mode;
            CurrentDevice = getinfo.Info.Name;
            CurrentUser = getinfo.CurrentUser.Name;
            if (mode == "首件")
                CurrentProduct = $"產品: {getinfo.CurrentProduct.Name}";
            else
                CurrentProduct = $"時段: {getinfo.InspectionStatuses.CurrentRoutine + 1}";

            selectionDescription = ErrorString.FirstOrDefault() ?? "";

            ConfirmCommand = new RelayCommand(() => OnConfirm());
            CancelCommand = new RelayCommand(() => OnCancel());
        }

        protected override void OnConfirm()
        {
            if (InspectionCheck is InspectionRadioCheck.Success)
                Result = new InspectionResult() { IsNormal = true };
            else
            { 
                if (IsOtherSelected)
                    Result = new InspectionResult()
                    { IsNormal = false, Description = $"{SelectionDescription}: {OtherDescription}" };
                else
                    Result = new InspectionResult()
                    { IsNormal = false, Description = SelectionDescription };
            }

            IsConfirmed = true;
            OnRequestClose();
        }

    }
}
