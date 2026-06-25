using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceDrivers.Abb;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    /// <summary>
    /// ABB 機械手設定頁（HardwareView 的一個 Tab）。管理多張設備卡片，
    /// 並以一個共用的掃描用 <see cref="IAbbRobotClient"/> 探索網路上的控制器，結果供各卡片共用。
    /// </summary>
    public partial class AbbRobotSettingViewModel : ObservableObject, IDisposable
    {
        private readonly Func<IAbbRobotClient> _clientFactory;
        private readonly IAbbRobotClient _scanner;   // 僅用於 DiscoverControllers，不連線
        private readonly HashSet<string> _registeredRemotes = new();   // 已 AddRemote 的 IP（去重）
        private bool _disposed;

        [ObservableProperty] private ObservableCollection<AbbRobotEntryViewModel> robots = new();
        [ObservableProperty] private bool isScanning;
        [ObservableProperty] private string? scanMessage;

        /// <summary>共用的控制器掃描結果，供各卡片的 IP ComboBox 取用。</summary>
        public ObservableCollection<AbbControllerInfo> DiscoveredControllers { get; } = new();

        public IAsyncRelayCommand LoadCommand { get; }
        public IAsyncRelayCommand RescanCommand { get; }
        public IRelayCommand AddCommand { get; }

        public AbbRobotSettingViewModel(Func<IAbbRobotClient> clientFactory)
        {
            _clientFactory = clientFactory;
            _scanner = clientFactory();
            LoadCommand = new AsyncRelayCommand(LoadAsync);
            RescanCommand = new AsyncRelayCommand(() => DiscoverAsync());
            AddCommand = new RelayCommand(AddRobot);
        }

        /// <summary>進入 Tab 時觸發：探索一次，若尚無卡片則自動新增一張。</summary>
        private async Task LoadAsync()
        {
            await DiscoverAsync().ConfigureAwait(true);
            if (Robots.Count == 0) AddRobot();
        }

        private async Task DiscoverAsync(params string[] hints)
        {
            IsScanning = true;
            try
            {
                var found = await Task.Run(() => _scanner.DiscoverControllers(hints)).ConfigureAwait(true);
                DiscoveredControllers.Clear();
                foreach (var c in found) DiscoveredControllers.Add(c);
                ScanMessage = string.Format(Properties.Resources.HwControllersFound, found.Count);
            }
            catch (AbbRobotException ex)
            {
                ScanMessage = $"探索失敗：{ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        /// <summary>
        /// 卡片要求以指定 IP 加入 remote 並重掃（同網段掃不到的控制器）。
        /// <c>AddRemoteController</c> 為進程全域且無法移除，故同一 IP 只註冊一次，之後僅重掃。
        /// </summary>
        private Task DiscoverWithHintAsync(string ipHint) =>
            _registeredRemotes.Add(ipHint)
                ? DiscoverAsync(ipHint)   // 首次 → 傳 hint（驅動內 AddRemoteController）+ 重掃
                : DiscoverAsync();        // 已註冊 → 只重掃，不重複 AddRemote

        private void AddRobot()
        {
            var entry = new AbbRobotEntryViewModel(
                _clientFactory(),
                DiscoveredControllers,
                DiscoverWithHintAsync,
                e => { Robots.Remove(e); e.Dispose(); });
            Robots.Add(entry);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var r in Robots) r.Dispose();
            Robots.Clear();
            _scanner.Dispose();
        }
    }
}
