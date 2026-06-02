using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceDrivers.Abb;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;

namespace FProductionDashBoard.ViewModels
{
    /// <summary>
    /// 單一 ABB 機械手設定卡片。負責一台控制器的 IP 選取、連線、狀態顯示，
    /// 以及連線後的 Task / Module / RAPID 變數列舉。每張卡片持有獨立的 <see cref="IAbbRobotClient"/>。
    /// </summary>
    public partial class AbbRobotEntryViewModel : ObservableObject, IDisposable
    {
        private readonly IAbbRobotClient _client;
        private readonly ObservableCollection<AbbControllerInfo> _discovered;   // 父層共用掃描結果
        private readonly Func<string, Task> _discoverWithHint;                   // 觸發父層加 remote + 重掃
        private readonly Action<AbbRobotEntryViewModel> _onDelete;

        private IReadOnlyList<AbbRapidSymbolInfo> _allRapidSymbols = Array.Empty<AbbRapidSymbolInfo>();
        private bool _disposed;

        // ── IP 選取 ──────────────────────────────────────────────────────────
        [ObservableProperty] private string? ipFilterText;
        [ObservableProperty] private AbbControllerInfo? selectedController;
        public ObservableCollection<AbbControllerInfo> FilteredControllers { get; } = new();

        // ── 狀態 chip ────────────────────────────────────────────────────────
        [ObservableProperty] private bool isConnected;
        [ObservableProperty] private AbbControllerState controllerState;
        [ObservableProperty] private AbbOperatingMode operatingMode;
        [ObservableProperty] private AbbExecutionStatus executionStatus;
        [ObservableProperty] private string? statusMessage;

        // ── Task / Module / RAPID 連動 ──────────────────────────────────────
        public ObservableCollection<AbbTaskInfo> Tasks { get; } = new();
        [ObservableProperty] private AbbTaskInfo? selectedTask;

        [ObservableProperty] private string? moduleFilterText;
        public ObservableCollection<string> FilteredModules { get; } = new();
        [ObservableProperty] private string? selectedModule;

        [ObservableProperty] private string? rapidFilterText;
        public ObservableCollection<AbbRapidSymbolInfo> FilteredRapidSymbols { get; } = new();
        [ObservableProperty] private AbbRapidSymbolInfo? selectedRapidSymbol;

        public IAsyncRelayCommand ConnectCommand { get; }
        public IRelayCommand DisconnectCommand { get; }
        public IRelayCommand DeleteCommand { get; }

        public AbbRobotEntryViewModel(
            IAbbRobotClient client,
            ObservableCollection<AbbControllerInfo> discovered,
            Func<string, Task> discoverWithHint,
            Action<AbbRobotEntryViewModel> onDelete)
        {
            _client = client;
            _discovered = discovered;
            _discoverWithHint = discoverWithHint;
            _onDelete = onDelete;

            _client.StatusChanged += OnStatusChanged;
            _discovered.CollectionChanged += OnDiscoveredChanged;

            ConnectCommand = new AsyncRelayCommand(ConnectAsync);
            DisconnectCommand = new RelayCommand(Disconnect);
            DeleteCommand = new RelayCommand(Delete);

            RebuildFilteredControllers();
        }

        // ── IP 篩選 ─────────────────────────────────────────────────────────
        partial void OnIpFilterTextChanged(string? value)
        {
            RebuildFilteredControllers();

            // 篩選後無結果，且輸入文字為合法 IP → 加入 remote 並重掃
            if (FilteredControllers.Count == 0
                && !string.IsNullOrWhiteSpace(value)
                && IPAddress.TryParse(value.Trim(), out _))
            {
                _ = _discoverWithHint(value.Trim());
            }
        }

        private void OnDiscoveredChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => RebuildFilteredControllers();

        private void RebuildFilteredControllers()
        {
            var filter = IpFilterText?.Trim();
            var matches = string.IsNullOrEmpty(filter)
                ? _discovered
                : _discovered.Where(c =>
                    c.IpAddress.Contains(filter, StringComparison.OrdinalIgnoreCase));

            FilteredControllers.Clear();
            foreach (var c in matches) FilteredControllers.Add(c);
        }

        // ── 連線 / 斷線 ──────────────────────────────────────────────────────
        private async Task ConnectAsync()
        {
            if (SelectedController is null) { StatusMessage = "請先選擇控制器"; return; }
            try
            {
                var ctrl = SelectedController;
                await Task.Run(() => _client.Connect(ctrl)).ConfigureAwait(true);
                StatusMessage = $"已連線 {ctrl.IpAddress}";

                // 連線成功後載入 Task 清單，預設選第一個（觸發 Module / RAPID 連動）
                var tasks = await Task.Run(() => _client.GetTasks()).ConfigureAwait(true);
                Tasks.Clear();
                foreach (var t in tasks) Tasks.Add(t);
                SelectedTask = Tasks.FirstOrDefault();
            }
            catch (AbbRobotException ex)
            {
                StatusMessage = $"連線失敗：{ex.Message}";
            }
        }

        private void Disconnect()
        {
            _client.Disconnect();
            Tasks.Clear();
            FilteredModules.Clear();
            FilteredRapidSymbols.Clear();
            SelectedTask = null;
            SelectedModule = null;
            SelectedRapidSymbol = null;
            StatusMessage = "已斷線";
        }

        // ── Task → Module 連動 ──────────────────────────────────────────────
        partial void OnSelectedTaskChanged(AbbTaskInfo? value)
        {
            ModuleFilterText = null;
            RebuildFilteredModules();
            SelectedModule = FilteredModules.FirstOrDefault();
        }

        partial void OnModuleFilterTextChanged(string? value) => RebuildFilteredModules();

        private void RebuildFilteredModules()
        {
            var modules = SelectedTask?.Modules ?? Array.Empty<string>();
            var filter = ModuleFilterText?.Trim();
            var matches = string.IsNullOrEmpty(filter)
                ? modules
                : modules.Where(m => m.Contains(filter, StringComparison.OrdinalIgnoreCase));

            FilteredModules.Clear();
            foreach (var m in matches) FilteredModules.Add(m);
        }

        // ── Module → RAPID 連動 ─────────────────────────────────────────────
        partial void OnSelectedModuleChanged(string? value) => _ = LoadRapidSymbolsAsync();

        private async Task LoadRapidSymbolsAsync()
        {
            if (SelectedTask is null || string.IsNullOrEmpty(SelectedModule))
            {
                _allRapidSymbols = Array.Empty<AbbRapidSymbolInfo>();
                RebuildFilteredRapidSymbols();
                return;
            }
            try
            {
                var taskName = SelectedTask.Name;
                var moduleName = SelectedModule;
                _allRapidSymbols = await Task.Run(
                    () => _client.GetModuleVariables(taskName, moduleName)).ConfigureAwait(true);
                RebuildFilteredRapidSymbols();
                SelectedRapidSymbol = FilteredRapidSymbols.FirstOrDefault();
            }
            catch (AbbRobotException ex)
            {
                StatusMessage = $"讀取變數失敗：{ex.Message}";
            }
        }

        partial void OnRapidFilterTextChanged(string? value) => RebuildFilteredRapidSymbols();

        private void RebuildFilteredRapidSymbols()
        {
            var filter = RapidFilterText?.Trim();
            var matches = string.IsNullOrEmpty(filter)
                ? _allRapidSymbols
                : _allRapidSymbols.Where(s =>
                    s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

            FilteredRapidSymbols.Clear();
            foreach (var s in matches) FilteredRapidSymbols.Add(s);
        }

        // ── 狀態事件（ABB SDK 內部執行緒觸發，需 dispatch 至 UI 執行緒）──────────
        private void OnStatusChanged(object? sender, AbbRobotStatus status)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                IsConnected     = status.IsConnected;
                ControllerState = status.State;
                OperatingMode   = status.OperatingMode;
                ExecutionStatus = status.RapidExecutionStatus;
            });
        }

        private void Delete()
        {
            _onDelete(this);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client.StatusChanged -= OnStatusChanged;
            _discovered.CollectionChanged -= OnDiscoveredChanged;
            _client.Disconnect();
            _client.Dispose();
        }
    }
}
