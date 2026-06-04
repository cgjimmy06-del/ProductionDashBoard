using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceDrivers.Modbus;
using System;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class ModbusTcpEntryViewModel : ObservableObject, IDisposable
    {
        private readonly Func<string, int, byte, IModbusClient> _factory;
        private readonly Action<ModbusTcpEntryViewModel> _onDelete;
        private IModbusClient? _client;
        private bool _disposed;

        [ObservableProperty] private string ipAddress = "192.168.1.1";
        [ObservableProperty] private int port = 502;
        [ObservableProperty] private int unitId = 1;
        [ObservableProperty] private bool isConnected;
        [ObservableProperty] private string statusMessage = "";

        public IAsyncRelayCommand ConnectCommand { get; }
        public IRelayCommand DisconnectCommand { get; }
        public IRelayCommand DeleteCommand { get; }

        public ModbusTcpEntryViewModel(
            Func<string, int, byte, IModbusClient> factory,
            Action<ModbusTcpEntryViewModel> onDelete)
        {
            _factory = factory;
            _onDelete = onDelete;
            ConnectCommand    = new AsyncRelayCommand(ConnectAsync);
            DisconnectCommand = new RelayCommand(Disconnect);
            DeleteCommand     = new RelayCommand(Delete);
        }

        private async Task ConnectAsync()
        {
            StatusMessage = "";
            try
            {
                await Task.Run(() =>
                {
                    _client?.Dispose();
                    _client = _factory(IpAddress, Port, (byte)UnitId);
                    _client.Connect();
                }).ConfigureAwait(true);

                IsConnected = _client!.IsConnected;
            }
            catch (ModbusClientException ex)
            {
                IsConnected   = false;
                StatusMessage = ex.Message;
            }
        }

        private void Disconnect()
        {
            try { _client?.Disconnect(); } catch { }
            IsConnected   = _client?.IsConnected ?? false;
            StatusMessage = "";
        }

        private void Delete() => _onDelete(this);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client?.Dispose();
        }
    }
}
