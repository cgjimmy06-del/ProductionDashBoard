using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using System.IO.Ports;

namespace FProductionDashBoard.ViewModels
{
    public partial class CardReaderEntryViewModel : ObservableObject
    {
        private readonly CardReaderService _reader;
        private readonly MultiCardReaderService _multi;
        private readonly Action<CardReaderEntryViewModel> _onDelete;

        public IReadOnlyList<int> BaudRateOptions { get; } =
            new[] { 9600, 19200, 38400, 57600, 115200, 230400 };

        [ObservableProperty] private string? selectedPort;
        [ObservableProperty] private int selectedBaudRate;
        [ObservableProperty] private bool isConnected;
        [ObservableProperty] private string? statusMessage;

        public IRelayCommand ConnectCommand { get; }
        public IRelayCommand TestCommand { get; }
        public IRelayCommand DeleteCommand { get; }

        public CardReaderEntryViewModel(CardReaderService reader, MultiCardReaderService multi,
            Action<CardReaderEntryViewModel> onDelete)
        {
            _reader = reader;
            _multi = multi;
            _onDelete = onDelete;
            SelectedPort = reader.PortName;
            SelectedBaudRate = reader.BaudRate;
            IsConnected = reader.IsConnected;
            ConnectCommand = new RelayCommand(Connect);
            TestCommand = new RelayCommand(Test);
            DeleteCommand = new RelayCommand(Delete);
        }

        private void Connect()
        {
            if (string.IsNullOrEmpty(SelectedPort)) { StatusMessage = Properties.Resources.HwSelectComPort; return; }
            _reader.Restart(SelectedPort, SelectedBaudRate);
            IsConnected = _reader.IsConnected;
            StatusMessage = IsConnected
                ? string.Format(Properties.Resources.HwConnected, SelectedPort, SelectedBaudRate)
                : Properties.Resources.HwConnectionFailed;

        }

        private void Test()
        {
            if (string.IsNullOrEmpty(SelectedPort)) { StatusMessage = Properties.Resources.HwSelectComPort; return; }
            try
            {
                using var p = new SerialPort(SelectedPort, SelectedBaudRate) { ReadTimeout = 500 };
                p.Open();
                StatusMessage = $"{SelectedPort} 可連線";
            }
            catch (Exception ex) { StatusMessage = $"無法連線: {ex.Message}"; }
        }

        private void Delete()
        {
            _multi.RemoveReader(_reader);
            _onDelete(this);
        }
    }
}
