using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FProductionDashBoard.ViewModels
{
    public enum InspectionRadioCheck
    {
        Success,
        Failure
    }
    public class InspectionResult
    {
        public bool IsNormal { get; set; } = false;
        public string? ErrorCode { get; set; }
        public string? Description { get; set; }
    }

    public partial class InspectionDialogViewModel : DialogBaseViewModel<InspectionResult>
    {
        [ObservableProperty]
        private string? currentDevice;
        [ObservableProperty]
        private string? currentProduct;
        [ObservableProperty]
        private string? currentUser;
        public ObservableCollection<ErrorInfo> ErrorCodes { get; } = new ();
        [ObservableProperty]
        private InspectionRadioCheck inspectionCheck = InspectionRadioCheck.Success;
        [ObservableProperty]
        private string selectionCode;
        [ObservableProperty]
        private string otherDescription = "";
        public bool IsOtherSelected => SelectionCode == (ErrorCodes.LastOrDefault() ?? new()).ErrorCode;
        partial void OnSelectionCodeChanged(string value)
        { OnPropertyChanged(nameof(IsOtherSelected)); }
        public InspectionDialogViewModel(string dialogstring, DeviceCardViewModel getinfo,
            List<ErrorInfo> sqlerrorslist) : base(dialogstring)
        {
            CurrentDevice = $"{Properties.Resources.ComStrDevice}: {getinfo.Info.Name}";
            CurrentUser = $"{Properties.Resources.ComStrUser}: {getinfo.CurrentUser.Name}";
            CurrentProduct = $"{Properties.Resources.ComStrProduct}: {getinfo.CurrentProduct.Name}";

            var categories = new List<int> { 1, 2 };
            ErrorCodes = new ObservableCollection<ErrorInfo>(sqlerrorslist.Where(e => categories.Contains(e.TypeId ?? 1)));
            SelectionCode = (ErrorCodes.FirstOrDefault() ?? new()).ErrorCode;

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
                    { IsNormal = false, ErrorCode = SelectionCode, Description = OtherDescription };
                else
                    Result = new InspectionResult()
                    { IsNormal = false, ErrorCode = SelectionCode, 
                        Description = ErrorCodes.FirstOrDefault(e => e.ErrorCode == SelectionCode)?.Message ?? ""
                    };
            }

            base.OnConfirm();
        }
    }
}
