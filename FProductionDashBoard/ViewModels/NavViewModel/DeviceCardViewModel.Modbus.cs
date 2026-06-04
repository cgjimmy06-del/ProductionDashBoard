using CommunityToolkit.Mvvm.ComponentModel;
using DeviceDrivers.Modbus;
using FProductionDashBoard.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FProductionDashBoard.ViewModels
{
    public partial class DeviceCardViewModel
    {
        private IModbusClient? _modbusClient;
        private CancellationTokenSource? _modbusLoopCts;
        private Task? _modbusLoopTask;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DeviceStatusLevel))]
        private bool isModbusConnected;

        private void InitModbus(IModbusClient? modbusClient)
        {
            if (modbusClient == null) return;
            _modbusClient = modbusClient;
            StartModbusLoop();
        }

        private void StartModbusLoop()
        {
            _modbusLoopCts = new CancellationTokenSource();
            _modbusLoopTask = RunModbusLoopAsync(_modbusLoopCts.Token);
        }

        private async Task RunModbusLoopAsync(CancellationToken ct)
        {
            await TryModbusConnectAsync().ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(10000, ct).ConfigureAwait(false); }
                catch { break; }
                if (!_modbusClient!.IsConnected)
                    await TryModbusConnectAsync().ConfigureAwait(false);
            }
        }

        private async Task TryModbusConnectAsync()
        {
            try
            {
                await Task.Run(() => _modbusClient!.Connect()).ConfigureAwait(false);
                _core.Log.AddLog($"[Modbus] {Info.Name} ({Info.IP}) 連線成功。");
            }
            catch (ModbusClientException ex)
            {
                _core.Log.AddLog($"[Modbus] {Info.Name} ({Info.IP}) 連線失敗，請檢查連線。", LogLevel.Error);
                _core.Log.AddErrorLog($"[TryModbusConnectAsync] {ex.Message}");
            }
            finally
            {
                bool connected = _modbusClient!.IsConnected;
                _ = Application.Current.Dispatcher.BeginInvoke(() => IsModbusConnected = connected);
            }
        }

        private void DisposeModbus()
        {
            _modbusLoopCts?.Cancel();
            var client = _modbusClient;
            var loop = _modbusLoopTask;
            _ = Task.Run(async () =>
            {
                try { if (loop != null) await loop.ConfigureAwait(false); }
                catch { }
                try { client?.Disconnect(); client?.Dispose(); }
                catch { }
            });
            _modbusLoopCts?.Dispose();
            _modbusLoopCts = null;
        }
    }
}
