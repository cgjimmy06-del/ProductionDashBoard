using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class CardReaderSettingViewModel : ObservableObject
    {
        private readonly MultiCardReaderService _multi;

        [ObservableProperty] private ObservableCollection<CardReaderEntryViewModel> readers = new();
        [ObservableProperty] private ObservableCollection<string> availablePorts = new();

        public ICommand LoadCommand { get; }
        public ICommand RefreshPortsCommand { get; }
        public ICommand AddCommand { get; }

        public CardReaderSettingViewModel(MultiCardReaderService multi)
        {
            _multi = multi;
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
            var defaultPort = AvailablePorts.FirstOrDefault() ?? "COM3";
            var reader = _multi.AddReader(defaultPort, 115200);
            Readers.Add(CreateEntry(reader));
        }

        private CardReaderEntryViewModel CreateEntry(CardReaderService reader) =>
            new(reader, _multi, e => Readers.Remove(e));
    }
}
