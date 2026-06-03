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
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanWrite))]
        private AbbOperatingMode operatingMode;
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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedRapidSymbol), nameof(CanWrite))]
        private AbbRapidSymbolInfo? selectedRapidSymbol;

        // ── 讀寫 ────────────────────────────────────────────────────────────
        [ObservableProperty] private string? rapidValue;
        [ObservableProperty] private string? newValueText;

        public bool HasSelectedRapidSymbol => SelectedRapidSymbol is not null;

        /// <summary>VAR / PERS、型別為 bool / num / string 或陣列，且非 Auto 模式時才可寫入。</summary>
        public bool CanWrite =>
            SelectedRapidSymbol?.Kind is "VAR" or "PERS" &&
            (SelectedRapidSymbol?.DataType is "bool" or "num" or "string"
             || SelectedRapidSymbol?.IsArray == true) &&
            OperatingMode != AbbOperatingMode.Auto;

        public IAsyncRelayCommand ConnectCommand { get; }
        public IRelayCommand DisconnectCommand { get; }
        public IRelayCommand DeleteCommand { get; }
        public IAsyncRelayCommand ReadValueCommand { get; }
        public IAsyncRelayCommand WriteValueCommand { get; }

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

            ConnectCommand    = new AsyncRelayCommand(ConnectAsync);
            DisconnectCommand = new RelayCommand(Disconnect);
            DeleteCommand     = new RelayCommand(Delete);
            ReadValueCommand  = new AsyncRelayCommand(ReadValueAsync);
            WriteValueCommand = new AsyncRelayCommand(WriteValueAsync);

            RebuildFilteredControllers();
        }

        // ── IP 篩選 ─────────────────────────────────────────────────────────
        // 僅做清單過濾；remote 註冊改在「按連線」時才觸發（見 ConnectAsync），
        // 避免逐字輸入時對進程全域且無法移除的 AddRemoteController 反覆註冊中間 IP。
        partial void OnIpFilterTextChanged(string? value) => RebuildFilteredControllers();

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
            var target = SelectedController;

            // 未選控制器但篩選框是合法 IP → 此時才加入 remote 並重掃，再選取該 IP（原需求「加入 remote 並嘗試連線」）
            if (target is null)
            {
                var ip = IpFilterText?.Trim();
                if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out _))
                {
                    StatusMessage = "請選擇控制器或輸入有效 IP";
                    return;
                }
                await _discoverWithHint(ip).ConfigureAwait(true);
                target = _discovered.FirstOrDefault(c => c.IpAddress == ip);
                if (target is null)
                {
                    StatusMessage = $"找不到控制器 {ip}";
                    return;
                }
                SelectedController = target;
            }

            try
            {
                var ctrl = target;
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
            IsConnected = false;
            ControllerState = AbbControllerState.Unknown;
            OperatingMode = AbbOperatingMode.Unknown;
            ExecutionStatus = AbbExecutionStatus.Unknown;
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

        partial void OnSelectedRapidSymbolChanged(AbbRapidSymbolInfo? value)
        {
            RapidValue = null;
            NewValueText = null;
        }

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

        // ── RAPID 讀寫 ───────────────────────────────────────────────────────
        private async Task ReadValueAsync()
        {
            if (SelectedRapidSymbol is null || SelectedTask is null || SelectedModule is null) return;
            var addr = new RapidVariableAddress(SelectedTask.Name, SelectedModule, SelectedRapidSymbol.Name);
            try
            {
                RapidValue = SelectedRapidSymbol.IsArray
                    ? await Task.Run(() => _client.ReadArray(addr))
                    : SelectedRapidSymbol.DataType switch
                    {
                        "bool"   => (await Task.Run(() => _client.ReadBool(addr))).ToString().ToLower(),
                        "num"    => (await Task.Run(() => _client.ReadNum(addr))).ToString(),
                        "string" => await Task.Run(() => _client.ReadString(addr)),
                        _        => "（不支援）"
                    };
            }
            catch (AbbRobotException ex)
            {
                RapidValue = "讀取失敗";
                StatusMessage = $"讀取失敗：{ex.Message}";
            }
        }

        private async Task WriteValueAsync()
        {
            if (SelectedRapidSymbol is null || SelectedTask is null || SelectedModule is null) return;
            if (OperatingMode == AbbOperatingMode.Auto)
            {
                StatusMessage = "Auto 模式下無法寫入變數";
                return;
            }
            var addr = new RapidVariableAddress(SelectedTask.Name, SelectedModule, SelectedRapidSymbol.Name);

            var confirm = MessageBox.Show(
                $"確定要將 {SelectedRapidSymbol.Name} 的值改為「{NewValueText}」？",
                "確認寫入", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;

            try
            {
                if (SelectedRapidSymbol.IsArray)
                {
                    await Task.Run(() => _client.WriteArray(addr, NewValueText ?? string.Empty));
                    StatusMessage = "寫入成功";
                    await ReadValueAsync();
                    return;
                }

                switch (SelectedRapidSymbol.DataType)
                {
                    case "bool":
                        if (!bool.TryParse(NewValueText, out var bv))
                        { StatusMessage = "請輸入 true 或 false"; return; }
                        await Task.Run(() => _client.WriteBool(addr, bv));
                        break;
                    case "num":
                        if (!double.TryParse(NewValueText,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var nv))
                        { StatusMessage = "請輸入數字"; return; }
                        await Task.Run(() => _client.WriteNum(addr, nv));
                        break;
                    case "string":
                        await Task.Run(() => _client.WriteString(addr, NewValueText ?? string.Empty));
                        break;
                    default: return;
                }
                StatusMessage = "寫入成功";
                await ReadValueAsync();
            }
            catch (AbbRobotException ex) { StatusMessage = $"寫入失敗：{ex.Message}"; }
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
