using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public class TuningResult
    {
        public TuningType TuningType { get; set; }
    }

    public partial class TuningDialogViewModel : DialogBaseViewModel<TuningResult>
    {
        public string CurrentDevice { get; }
        public string CurrentUser { get; }
        public string CurrentProduct { get; }

        [ObservableProperty]
        private int tuningMode = (int)TuningType.Teaching; // ±aÂI¼Ò¦¡

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

            Result = new TuningResult { TuningType = TuningType.Teaching };
        }

        private void SelectTeaching()
        {
            Result = new TuningResult { TuningType = TuningType.Teaching };
            TuningMode = (int)TuningType.Teaching;
        }

        private void SelectOffset()
        {
            Result = new TuningResult { TuningType = TuningType.Offset };
            TuningMode = (int)TuningType.Offset;
        }
    }
}
