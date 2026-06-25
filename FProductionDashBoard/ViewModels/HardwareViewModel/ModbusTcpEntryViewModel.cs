using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceDrivers.Modbus;
using DeviceDrivers.Modbus.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace FProductionDashBoard.ViewModels
{
    public partial class ModbusTcpEntryViewModel : ObservableObject, IDisposable
    {
        private readonly Func<string, int, byte, IModbusClient> _factory;
        private readonly Action<ModbusTcpEntryViewModel> _onDelete;
        private IModbusClient? _client;
        private bool _disposed;

        // ── 連線設定 ─────────────────────────────────────────────────
        [ObservableProperty] private string ipAddress = "192.168.1.1";
        [ObservableProperty] private int port = 502;
        [ObservableProperty] private int unitId = 0;
        [ObservableProperty] private bool isConnected;
        [ObservableProperty] private string statusMessage = "";

        // ── 下拉選項 ─────────────────────────────────────────────────
        public IReadOnlyList<ModbusRegisterType> RegisterTypeOptions { get; } =
            (ModbusRegisterType[])Enum.GetValues(typeof(ModbusRegisterType));

        public IReadOnlyList<ModbusValueFormat> ValueFormatOptions { get; } =
            (ModbusValueFormat[])Enum.GetValues(typeof(ModbusValueFormat));

        // ── 讀取參數 ─────────────────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanWrite))]
        private ModbusRegisterType selectedRegisterType = ModbusRegisterType.HoldingRegister;

        [ObservableProperty] private ushort readStartAddress;
        [ObservableProperty] private ushort readCount = 1;
        [ObservableProperty] private ModbusValueFormat selectedValueFormat = ModbusValueFormat.UInt16;
        public ObservableCollection<string> ReadResults { get; } = new();

        // ── 寫入參數 ─────────────────────────────────────────────────
        [ObservableProperty] private ushort writeAddress;
        [ObservableProperty] private string writeRawValue = "";

        public bool CanWrite =>
            SelectedRegisterType is ModbusRegisterType.Coil or ModbusRegisterType.HoldingRegister;

        // ── 命令 ─────────────────────────────────────────────────────
        public IAsyncRelayCommand ConnectCommand { get; }
        public IRelayCommand DisconnectCommand { get; }
        public IRelayCommand DeleteCommand { get; }
        public IAsyncRelayCommand ReadCommand { get; }
        public IAsyncRelayCommand WriteCommand { get; }

        public ModbusTcpEntryViewModel(
            Func<string, int, byte, IModbusClient> factory,
            Action<ModbusTcpEntryViewModel> onDelete)
        {
            _factory = factory;
            _onDelete = onDelete;
            ConnectCommand    = new AsyncRelayCommand(ConnectAsync);
            DisconnectCommand = new RelayCommand(Disconnect);
            DeleteCommand     = new RelayCommand(Delete);
            ReadCommand       = new AsyncRelayCommand(ReadAsync);
            WriteCommand      = new AsyncRelayCommand(WriteAsync);
        }

        // ── 連線 ─────────────────────────────────────────────────────
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
            ReadResults.Clear();
        }

        private void Delete() => _onDelete(this);

        // ── 讀取 ─────────────────────────────────────────────────────
        private async Task ReadAsync()
        {
            ReadResults.Clear();
            StatusMessage = "";
            try
            {
                if (SelectedRegisterType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput)
                {
                    var isCoil = SelectedRegisterType == ModbusRegisterType.Coil;
                    var bools = await Task.Run(() =>
                        isCoil
                            ? _client!.ReadCoils(ReadStartAddress, ReadCount)
                            : _client!.ReadDiscreteInputs(ReadStartAddress, ReadCount))
                        .ConfigureAwait(true);

                    for (int i = 0; i < bools.Length; i++)
                    {
                        ushort a = (ushort)(ReadStartAddress + i);
                        ReadResults.Add($"0x{a:X4}({a}): {bools[i]}");
                    }
                }
                else
                {
                    int stride = ModbusValueConverter.WordCount(SelectedValueFormat);
                    var wordsToRead = (ushort)(ReadCount * stride);
                    var isHolding = SelectedRegisterType == ModbusRegisterType.HoldingRegister;
                    var words = await Task.Run(() =>
                        isHolding
                            ? _client!.ReadHoldingRegisters(ReadStartAddress, wordsToRead)
                            : _client!.ReadInputRegisters(ReadStartAddress, wordsToRead))
                        .ConfigureAwait(true);

                    for (int i = 0; i < ReadCount; i++)
                    {
                        int offset = i * stride;
                        ushort addr = (ushort)(ReadStartAddress + offset);
                        string typed = ModbusValueConverter.Format(words, offset, SelectedValueFormat);
                        string raw = stride == 1
                            ? $"0x{words[offset]:X4}"
                            : $"0x{words[offset]:X4}{words[offset + 1]:X4}";
                        ReadResults.Add($"0x{addr:X4}({addr}): {typed}  (raw: {raw})");
                    }
                }
            }
            catch (ModbusClientException ex)
            {
                StatusMessage = ex.Message;
                IsConnected = _client?.IsConnected ?? false;
            }
        }

        // ── 寫入 ─────────────────────────────────────────────────────
        private async Task WriteAsync()
        {
            if (!CanWrite) return;

            var confirm = MessageBox.Show(
                string.Format(Properties.Resources.HwConfirmModbusWrite, WriteAddress.ToString("X4"), WriteRawValue),
                Properties.Resources.HwConfirmWriteTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;

            StatusMessage = "";
            try
            {
                if (SelectedRegisterType == ModbusRegisterType.Coil)
                {
                    if (!bool.TryParse(WriteRawValue, out bool bv))
                    { StatusMessage = Properties.Resources.HwValidationTrueFalse; return; }
                    await Task.Run(() => _client!.WriteSingleCoil(WriteAddress, bv)).ConfigureAwait(true);
                }
                else
                {
                    if (!ushort.TryParse(WriteRawValue, out ushort uv))
                    { StatusMessage = Properties.Resources.HwValidationIntRange; return; }
                    await Task.Run(() => _client!.WriteSingleRegister(WriteAddress, uv)).ConfigureAwait(true);
                }
            }
            catch (ModbusClientException ex)
            {
                StatusMessage = ex.Message;
                IsConnected = _client?.IsConnected ?? false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client?.Dispose();
        }
    }
}
