using CommunityToolkit.Mvvm.ComponentModel;
using DeviceDrivers.Abb;
using FProductionDashBoard.Services;
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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AbbStatusLevel))]
        private bool isAbbConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AbbStatusLevel))]
        private AbbControllerState abbControllerState;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AbbStatusLevel))]
        [NotifyPropertyChangedFor(nameof(IsAbbAutoMode))]
        private AbbOperatingMode abbOperatingMode;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AbbStatusLevel))]
        private AbbExecutionStatus abbExecutionStatus;

        /// <summary>
        /// 燈號等級，對映 IntToColorConverter（0=綠/Success, 1=黃/Warning, 2=紅/Error, other=灰/Idle）。
        /// 非 ABB 設備或未連線恆回 3（Idle）。優先序：錯誤狀態 > Running > 已連線預設。
        /// </summary>
        public int AbbStatusLevel =>
            !IsAbbConnected ? 3 :
            AbbControllerState is AbbControllerState.GuardStop or AbbControllerState.EmergencyStop
                or AbbControllerState.EmergencyStopReset or AbbControllerState.SystemFailure ? 2 :
            AbbExecutionStatus == AbbExecutionStatus.Running ? 0 :
            1;

        /// <summary>OperatingMode 為 Auto 時為 true，控制 Auto chip 可見性。</summary>
        public bool IsAbbAutoMode => AbbOperatingMode == AbbOperatingMode.Auto;

        private void InitAbb(IAbbRobotClient? abbClient)
        {
            if (abbClient == null || !IsValidAbbIp(Info.IP)) return;
            _abbClient = abbClient;
            _abbClient.StatusChanged += OnAbbStatusChanged;
            StartAbbLoop();
        }

        private static bool IsValidAbbIp(string? ip) =>
            System.Net.IPAddress.TryParse(ip, out _);

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
                try { await Task.Delay(10000, ct).ConfigureAwait(false); }
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
                _core.Log.AddLog($"[ABB] {Info.Name} ({Info.IP}) 連線失敗，請檢查連線。");
                _core.Log.AddErrorLog($"[TryAbbConnectAsync] {Info.Name} ({Info.IP}) {ex.Message}");
            }
        }

        private void OnAbbStatusChanged(object? sender, AbbRobotStatus status)
        {
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

        // ─── 生產 SeqNo 寫入（非通用，僅供接單流程；勿移作他用）───────────────────
        // 開始生產時把該訂單的 EquipmentProduct.SeqNo 寫入機器人指定 num 變數，
        // 結束生產（及啟動失敗復原）時寫回 0。位址常數待依現場 RAPID 程式填寫。

        /// <summary>
        /// 把指定 SeqNo 寫入機器人變數。連線/寫入失敗會 <b>拋出</b> <see cref="AbbRobotException"/>，
        /// 供開始生產流程判斷成敗（失敗則不更新訂單）。呼叫端需先確認為 ABB 設備（_abbClient 非 null）。
        /// </summary>
        private Task WriteSeqNoAsync(int seqNo)
        {
            var cfg = _hardwareConfig.Current;
            var addr = new RapidVariableAddress(cfg.AbbSeqNoTask, cfg.AbbSeqNoModule, cfg.AbbSeqNoVariable);
            return Task.Run(() => _abbClient!.WriteNum(addr, seqNo));
        }

        /// <summary>
        /// 嘗試把生產變數寫回 0；fire-and-forget，吞例外只記 log、不阻擋流程、不需成功。
        /// 用於結束生產，以及開始生產時「寫入成功但 DB 更新失敗」的復原。
        /// </summary>
        private void ResetSeqNoFireAndForget()
        {
            if (_abbClient == null) return;
            var cfg = _hardwareConfig.Current;
            var addr = new RapidVariableAddress(cfg.AbbSeqNoTask, cfg.AbbSeqNoModule, cfg.AbbSeqNoVariable);
            _ = Task.Run(() =>
            {
                try { _abbClient.WriteNum(addr, 0); }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"[{Info.Name}] 生產變數歸零失敗", LogLevel.Error);
                    _core.Log.AddErrorLog($"[ResetSeqNoFireAndForget] {ex.Message}");
                }
            });
        }
    }
}
