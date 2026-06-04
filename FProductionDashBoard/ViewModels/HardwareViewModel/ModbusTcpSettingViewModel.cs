using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceDrivers.Modbus;
using System;
using System.Collections.ObjectModel;

namespace FProductionDashBoard.ViewModels
{
    public partial class ModbusTcpSettingViewModel : ObservableObject, IDisposable
    {
        private readonly Func<string, int, byte, IModbusClient> _factory;
        private bool _disposed;

        [ObservableProperty] private ObservableCollection<ModbusTcpEntryViewModel> devices = new();

        public IRelayCommand LoadCommand { get; }
        public IRelayCommand AddCommand { get; }

        public ModbusTcpSettingViewModel(Func<string, int, byte, IModbusClient> factory)
        {
            _factory = factory;
            LoadCommand = new RelayCommand(Load);
            AddCommand  = new RelayCommand(AddDevice);
        }

        private void Load()
        {
            if (Devices.Count == 0) AddDevice();
        }

        private void AddDevice()
        {
            Devices.Add(new ModbusTcpEntryViewModel(_factory, e =>
            {
                Devices.Remove(e);
                e.Dispose();
            }));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var d in Devices) d.Dispose();
            Devices.Clear();
        }
    }
}
