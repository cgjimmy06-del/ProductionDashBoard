using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public class TuningResult
    {
        public TuningType TuningType { get; set; }
    }

    public class TuningDialogViewModel : DialogBaseViewModel<TuningResult>
    {
        public string CurrentDevice { get; }
        public string CurrentUser { get; }
        public string CurrentProduct { get; }

        public ICommand TeachingCommand { get; }
        public ICommand OffsetCommand { get; }

        public TuningDialogViewModel(string device, string user, string product)
            : base(Properties.Resources.TuningDialogTitle)
        {
            CurrentDevice = device;
            CurrentUser = user;
            CurrentProduct = product;
            TeachingCommand = new RelayCommand(SelectTeaching);
            OffsetCommand = new RelayCommand(SelectOffset);
        }

        private void SelectTeaching()
        {
            Result = new TuningResult { TuningType = TuningType.Teaching };
            OnConfirm();
        }

        private void SelectOffset()
        {
            Result = new TuningResult { TuningType = TuningType.Offset };
            OnConfirm();
        }
    }
}
