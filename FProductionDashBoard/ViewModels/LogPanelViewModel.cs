using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class LogPanelViewModel : ObservableObject
    {
        private readonly LogService _log;

        [ObservableProperty]
        private bool isErrorMode = false;
        [ObservableProperty]
        private bool autoScrollEnabled = true;
        [ObservableProperty]
        private bool isLargeFontMode = false;

        public LogService Log => _log;
        public ObservableCollection<LogEntry> CurrentLogs => IsErrorMode ? _log.ErrorLogs : _log.Logs;

        public ICommand SaveLogsCommand { get; }

        public LogPanelViewModel(LogService logService)
        {
            _log = logService;
            SaveLogsCommand = new AsyncRelayCommand(SaveLogsAsync);
        }

        partial void OnIsErrorModeChanged(bool value)
        {
            if (value && _log.IsNewErrorLog) _log.IsNewErrorLog = false;
            OnPropertyChanged(nameof(CurrentLogs));
        }

        private async Task SaveLogsAsync()
        {
            try
            {
                await _log.ExportInMemoryLogsAsync();
                _log.AddLog(Properties.Resources.MainProgressSuccess, LogLevel.Success);
            }
            catch (System.AggregateException ex)
            {
                _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: SaveLogs", LogLevel.Error);
                _log.AddErrorLog($"SaveLogs Aggre.Ex: {ex}");
            }
            catch (System.Exception ex)
            {
                _log.AddLog($"{Properties.Resources.ComStrErrorTitle}: SaveLogs", LogLevel.Error);
                _log.AddErrorLog($"SaveLogs Ex: {ex}");
            }
        }
    }
}
