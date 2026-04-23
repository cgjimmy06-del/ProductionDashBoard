using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public abstract partial class SettingViewModelBase : ObservableObject
    {
        protected readonly LogService _log;
        protected readonly IDataService _dataService;

        [ObservableProperty] private string? formErrorString;
        [ObservableProperty] private string? formSuccessString;
        [ObservableProperty] private bool isFormVisible = false;

        public ICommand LoadCommand { get; protected set; }
        public ICommand NewCommand { get; protected set; }
        public ICommand SaveCommand { get; protected set; }
        public ICommand CancelCommand { get; protected set; }

        protected SettingViewModelBase(LogService log, IDataService dataService)
        {
            _log = log;
            _dataService = dataService;

            LoadCommand = new AsyncRelayCommand(LoadAsync);
            NewCommand = new RelayCommand(OpenNewForm);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
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
    }
}
