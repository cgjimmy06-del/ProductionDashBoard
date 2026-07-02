using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FProductionDashBoard.ViewModels
{
    public partial class MainViewModel
    {
        public DispatcherTimer DefaultTimer = null!;

        [ObservableProperty] private int businessHour = 8;
        [ObservableProperty] private int businessMinute = 0;
        [ObservableProperty] private string currentTime = "";

        private int _cardRefreshTickCounter = 0;
        private bool _isCardRefreshRunning = false;
        private int _missedCheckCounter = 0;

        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        private int GetIdleSeconds()
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            GetLastInputInfo(ref info);
            return (int)((uint)Environment.TickCount - info.dwTime) / 1000;
        }

        partial void InitializeScheduler()
        {
            BusinessHour = _systemConfig.Current.BusinessHour;
            BusinessMinute = _systemConfig.Current.BusinessMinute;
            _core.Data.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);
            DefaultTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            DefaultTimer.Tick += OnTimerTick;
            DefaultTimer.Start();
        }

        partial void DisposeScheduler()
        {
            DefaultTimer.Stop();
            DefaultTimer.Tick -= OnTimerTick;
        }

        private async void OnTimerTick(object? s, EventArgs e) => await OnTimerTickAsync();
        private async Task OnTimerTickAsync()
        {
            CurrentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            if (DateTime.Now.AddDays(-1) > _core.Data.BusinessDay)
                _core.Data.BusinessDay = DateTime.Today.AddHours(BusinessHour).AddMinutes(BusinessMinute);

            OnPropertyChanged(nameof(IsCardReaderConnected));
            OnPropertyChanged(nameof(CardReaderStatusTooltip));

            var s = _systemConfig.Current;

            _cardRefreshTickCounter++;
            if (s.CardRefreshEnabled && _cardRefreshTickCounter >= s.CardRefreshIntervalSec && !_isCardRefreshRunning)
            {
                _cardRefreshTickCounter = 0;
                _isCardRefreshRunning = true;
                var containerSnapshot = BuildActiveContainerSnapshot();
                _ = Task.Run(async () =>
                {
                    try { await RefreshActiveCardsAsync(containerSnapshot); }
                    catch (Exception ex)
                    {
                        _core.Log.AddLog("[RefreshActiveCardsAsync] 卡片狀態刷新背景任務發生例外", LogLevel.Error);
                        _core.Log.AddErrorLog($"[RefreshActiveCardsAsync] {ex.Message}");
                    }
                    finally
                    { _isCardRefreshRunning = false; }
                });
            }

            _missedCheckCounter++;
            if (s.MissedCheckEnabled && _missedCheckCounter >= s.MissedCheckIntervalSec)
            {
                _missedCheckCounter = 0;
                var containerSnapshot = BuildActiveContainerSnapshot();
                _ = Task.Run(async () =>
                {
                    try { await CheckMissedInspectionsAsync(containerSnapshot); }
                    catch (Exception ex)
                    {
                        _core.Log.AddLog("[CheckMissedInspectionsAsync] 補填背景任務發生例外", LogLevel.Error);
                        _core.Log.AddErrorLog($"[CheckMissedInspectionsAsync] {ex.Message}");
                    }
                });
            }

            if (s.IdleLogoutEnabled && IsLoggedIn && GetIdleSeconds() >= s.IdleLogoutIntervalSec)
                await CheckLogOutForLongIdle();
        }

        private IReadOnlyList<OperationViewModel> BuildActiveContainerSnapshot()
        {
            return new[] { _deviceContainer }
                .Concat(_panelContainers.Values)
                .Where(c => c != null)
                .Cast<OperationViewModel>()
                .ToList();
        }

        private async Task RefreshActiveCardsAsync(IReadOnlyList<OperationViewModel> containers)
        {
            var activeDevices = containers.SelectMany(c => c.Devices).Distinct().ToList();
            if (!activeDevices.Any()) return;

            foreach (var card in activeDevices)
            {
                try { await card.RefreshCardStateAsync(); }
                catch (InvalidOperationException)
                {
                    _core.Log.AddLog("卡片狀態刷新略過：資料庫連線失敗", LogLevel.Warning);
                    break;
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"卡片狀態刷新失敗 [{card.Info.Name}]", LogLevel.Error);
                    _core.Log.AddErrorLog($"[RefreshActiveCardsAsync] [{card.Info.Name}] {ex.Message}");
                }
            }
        }

        private async Task CheckMissedInspectionsAsync(IReadOnlyList<OperationViewModel> containers)
        {
            if (CommonLists.TimeSlotsList.Count == 0) return;
            var activeDevices = containers
                .SelectMany(c => c.Devices)
                .Distinct()
                .ToList();
            if (!activeDevices.Any()) return;

            foreach (var card in activeDevices)
            {
                try
                {
                    await _core.Data.CheckAndInsertMissedInspectionAsync(CommonLists.TimeSlotsList, card.Info.Id);
                    await card.UpdateTimeSlotsStatusAsync();
                }
                catch (InvalidOperationException)
                {
                    _core.Log.AddLog("逾時補填/狀態更新略過：資料庫連線失敗", LogLevel.Warning);
                    break;
                }
                catch (Exception ex)
                {
                    _core.Log.AddLog($"逾時補填失敗 [{card.Info.Name}]", LogLevel.Error);
                    _core.Log.AddErrorLog($"[CheckMissedInspectionsAsync] [{card.Info.Name}] {ex.Message}");
                }
            }
        }

        private async Task CheckLogOutForLongIdle()
        {
            if (!IsLoggedIn) return;

            bool isExtendLogin = false;
            var vm = new LoadingViewModel
            {
                Mode = LoadingMode.LogoutCountdown,
                Message = "閒置逾時警告",
                CountdownSeconds = 10
            };
            vm.SessionExtended += (_, _) =>
            {
                isExtendLogin = true;
                _core.Log.AddLog("已延長，歡迎回來", LogLevel.Success);
            };
            var win = new LoadingWindow(vm);
            vm.StartCountdown();
            win.ShowDialog();

            if (!isExtendLogin)
                await _core.Authorization.LogoutAsync();
        }
    }
}
