using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class CardReaderSettingViewModel : ObservableObject
    {
        private readonly MultiCardReaderService _multi;
        private readonly IConfigService<HardwareConfigDto> _hardwareConfig;

        [ObservableProperty] private ObservableCollection<CardReaderEntryViewModel> readers = new();
        [ObservableProperty] private ObservableCollection<string> availablePorts = new();

        public ICommand LoadCommand { get; }
        public ICommand RefreshPortsCommand { get; }
        public ICommand AddCommand { get; }

        public CardReaderSettingViewModel(MultiCardReaderService multi, IConfigService<HardwareConfigDto> hardwareConfig)
        {
            _multi = multi;
            _hardwareConfig = hardwareConfig;
            LoadCommand = new RelayCommand(Load);
            RefreshPortsCommand = new RelayCommand(RefreshPorts);
            AddCommand = new RelayCommand(AddReader);
        }

        private void Load()
        {
            RefreshPorts();
            Readers = new ObservableCollection<CardReaderEntryViewModel>(
                _multi.Readers.Select(r => CreateEntry(r)));
        }

        private void RefreshPorts()
        {
            AvailablePorts = new ObservableCollection<string>(SerialPort.GetPortNames());
        }

        private void AddReader()
        {
            var cfg = _hardwareConfig.Current;
            var defaultPort = AvailablePorts.FirstOrDefault() ?? cfg.ReaderPort;
            var reader = _multi.AddReader(defaultPort, cfg.ReaderBaud);
            Readers.Add(CreateEntry(reader));
        }

        private CardReaderEntryViewModel CreateEntry(CardReaderService reader) =>
            new(reader, _multi, e => Readers.Remove(e));
    }
}
