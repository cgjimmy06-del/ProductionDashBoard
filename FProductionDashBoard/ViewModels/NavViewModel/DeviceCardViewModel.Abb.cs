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
        private Task? _abbLoopTask;

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
            _abbLoopTask = RunAbbLoopAsync(_abbLoopCts.Token);
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
            // 提前解訂閱，之後不再收到狀態事件
            if (_abbClient != null)
                _abbClient.StatusChanged -= OnAbbStatusChanged;

            _abbLoopCts?.Cancel();

            // 不在此處將 _abbClient / _abbLoopTask 設為 null：背景 loop 仍可能讀取 _abbClient，
            // 提前 null 會造成 NRE。改為捕獲到區域變數，待 loop（含進行中的 Connect）確實結束後
            // 才釋放 client，確保 Disconnect/Dispose 不與 Connect 並發；於背景執行不阻塞 UI。
            var client = _abbClient;
            var loop = _abbLoopTask;
            _ = Task.Run(async () =>
            {
                try { if (loop != null) await loop.ConfigureAwait(false); }
                catch { /* 取消或連線中例外，忽略 */ }
                try { client?.Disconnect(); client?.Dispose(); }
                catch { /* 釋放容錯 */ }
            });

            _abbLoopCts?.Dispose();
            _abbLoopCts = null;
        }
    }
}
