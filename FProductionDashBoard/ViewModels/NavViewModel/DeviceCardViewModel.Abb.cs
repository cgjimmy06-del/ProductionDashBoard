using CommunityToolkit.Mvvm.ComponentModel;
using DeviceDrivers.Abb;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FProductionDashBoard.ViewModels
{
    public partial class DeviceCardViewModel
    {
        private IAbbRobotClient? _abbClient;
        private AbbControllerInfo? _lastAbbControllerInfo;
        private CancellationTokenSource? _abbLoopCts;

        [ObservableProperty] private bool isAbbConnected;
        [ObservableProperty] private AbbControllerState abbControllerState;
        [ObservableProperty] private AbbOperatingMode abbOperatingMode;
        [ObservableProperty] private AbbExecutionStatus abbExecutionStatus;

        private void InitAbb(IAbbRobotClient? abbClient)
        {
            if (abbClient == null || !IsValidAbbIp(Info.IP)) return;
            _abbClient = abbClient;
            _abbClient.StatusChanged += OnAbbStatusChanged;
            StartAbbLoop();
        }

        private static bool IsValidAbbIp(string? ip) =>
            !string.IsNullOrWhiteSpace(ip) && ip != "none";

        private void StartAbbLoop()
        {
            _abbLoopCts = new CancellationTokenSource();
            _ = RunAbbLoopAsync(_abbLoopCts.Token);
        }

        private async Task RunAbbLoopAsync(CancellationToken ct)
        {
            await TryAbbConnectAsync().ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(5000, ct).ConfigureAwait(false); }
                catch { break; }
                if (!_abbClient!.IsConnected)
                    await TryAbbConnectAsync().ConfigureAwait(false);
            }
        }

        private async Task TryAbbConnectAsync()
        {
            try
            {
                var controllers = await Task.Run(() =>
                    _abbClient!.DiscoverControllers(Info.IP)).ConfigureAwait(false);
                _lastAbbControllerInfo = controllers.FirstOrDefault(c => c.IpAddress == Info.IP);
                if (_lastAbbControllerInfo != null)
                {
                    await Task.Run(() => _abbClient!.Connect(_lastAbbControllerInfo)).ConfigureAwait(false);
                    _core.Log.AddLog($"[ABB] {Info.Name} ({Info.IP}) 連線成功。");
                }
            }
            catch (AbbRobotException ex)
            {
                _core.Log.AddLog($"[ABB] {Info.Name} ({Info.IP}) 連線失敗。");
                _core.Log.AddErrorLog($"[TryAbbConnectAsync] {Info.Name} ({Info.IP}) {ex.Message}");
            }
        }

        private void OnAbbStatusChanged(object? sender, AbbRobotStatus status)
        {
            _core.Log.AddLog($"[ABB] {Info.Name} " +
                $"StateChanged -> " +
                $"IsConnected={status.IsConnected} " +
                $"State={status.State} " +
                $"OperatingMode={status.OperatingMode} " +
                $"RapidExecutionStatus={status.RapidExecutionStatus} ");
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                IsAbbConnected     = status.IsConnected;
                AbbControllerState = status.State;
                AbbOperatingMode   = status.OperatingMode;
                AbbExecutionStatus = status.RapidExecutionStatus;
            });
        }

        private void DisposeAbb()
        {
            _abbLoopCts?.Cancel();
            _abbLoopCts?.Dispose();
            _abbLoopCts = null;
            if (_abbClient != null)
            {
                _abbClient.StatusChanged -= OnAbbStatusChanged;
                _abbClient.Disconnect();
                _abbClient.Dispose();
            }
        }
    }
}
