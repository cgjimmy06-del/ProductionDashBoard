using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public abstract partial class SettingViewModelBase : ObservableObject
    {
        protected readonly DashboardCoreServices _core;
        private readonly Services.IDialogService _dialog;

        [ObservableProperty] private string? formErrorString;
        [ObservableProperty] private string? formSuccessString;
        [ObservableProperty] private bool isFormVisible = false;

        public ICommand LoadCommand { get; protected set; }
        public ICommand NewCommand { get; protected set; }
        public ICommand SaveCommand { get; protected set; }
        public ICommand CancelCommand { get; protected set; }

        protected SettingViewModelBase(DashboardCoreServices core, Services.IDialogService dialog)
        {
            _core = core;
            _dialog = dialog;

            LoadCommand = new AsyncRelayCommand(LoadAsync);
            NewCommand = new RelayCommand(OpenNewForm);
            SaveCommand = new AsyncRelayCommand(ConfirmAndSaveAsync);
            CancelCommand = new RelayCommand(CloseForm);
        }

        protected abstract Task LoadAsync();
        protected abstract void OpenNewForm();
        protected abstract Task SaveAsync();
        
        protected void CloseForm()
        {
            IsFormVisible = false;
            ClearForm();
            FormErrorString = null;
            FormSuccessString = null;
        }
        protected abstract void ClearForm();

        private async Task ConfirmAndSaveAsync()
        {
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseSave}?")) 
                return;
            await SaveAsync();
        }
        protected bool ShowConfirm(string message) => _dialog.ShowConfirm(message);
        protected TResult? ShowDialog<TResult>(DialogBaseViewModel<TResult> vm) => _dialog.ShowDialog(vm);
    }
}
