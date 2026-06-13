using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using FProductionDashBoard.Services.WebApi;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class SystemSettingsViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;
        private readonly ILogUploadService _logUpload;
        private readonly IConfigService<SystemConfigDto> _systemConfig;
        private readonly IConfigService<HardwareConfigDto> _hardwareConfig;

        private readonly PaletteHelper _paletteHelper = new PaletteHelper();
        private readonly Theme _lightTheme = Theme.Create(BaseTheme.Light,
            SwatchHelper.Lookup[MaterialDesignColor.Indigo], SwatchHelper.Lookup[MaterialDesignColor.Lime]);
        private readonly Theme _darkTheme = Theme.Create(BaseTheme.Dark,
            SwatchHelper.Lookup[MaterialDesignColor.Indigo], SwatchHelper.Lookup[MaterialDesignColor.Lime]);

        // 外觀設定
        public string CurrentLanguage => Properties.Settings.Default.CultureCode;
        public bool CurrentIsDark => Properties.Settings.Default.IsDarkMode;
        public bool CurrentIsLight => !Properties.Settings.Default.IsDarkMode;

        public IRelayCommand<string> SetLanguageCommand { get; }
        public IRelayCommand<string> SetThemeCommand { get; }

        // 系統參數
        [ObservableProperty] private bool hasUnsavedChanges     = false;
        [ObservableProperty] private int  businessHour           = 8;
        [ObservableProperty] private int  businessMinute         = 0;
        [ObservableProperty] private bool syncEnabled            = true;
        [ObservableProperty] private int  syncIntervalSec        = 60;
        [ObservableProperty] private bool missedCheckEnabled     = true;
        [ObservableProperty] private int  missedCheckIntervalSec = 300;
        [ObservableProperty] private bool idleLogoutEnabled      = true;
        [ObservableProperty] private int  idleLogoutIntervalSec  = 600;
        [ObservableProperty] private bool skipLoginScreen        = false;

        partial void OnBusinessHourChanged(int value)           => HasUnsavedChanges = true;
        partial void OnBusinessMinuteChanged(int value)         => HasUnsavedChanges = true;
        partial void OnSyncEnabledChanged(bool value)           => HasUnsavedChanges = true;
        partial void OnSyncIntervalSecChanged(int value)        => HasUnsavedChanges = true;
        partial void OnMissedCheckEnabledChanged(bool value)    => HasUnsavedChanges = true;
        partial void OnMissedCheckIntervalSecChanged(int value) => HasUnsavedChanges = true;
        partial void OnIdleLogoutEnabledChanged(bool value)     => HasUnsavedChanges = true;
        partial void OnIdleLogoutIntervalSecChanged(int value)  => HasUnsavedChanges = true;
        partial void OnSkipLoginScreenChanged(bool value)       => HasUnsavedChanges = true;

        // 硬體設定
        [ObservableProperty] private string readerPort       = "COM3";
        [ObservableProperty] private int    readerBaud        = 115200;
        [ObservableProperty] private string abbSeqNoTask     = "T_ROB1";
        [ObservableProperty] private string abbSeqNoModule   = "MES";
        [ObservableProperty] private string abbSeqNoVariable = "MES_project";

        [ObservableProperty] private bool deviceReconnectEnabled     = true;
        [ObservableProperty] private int  deviceReconnectIntervalSec = 10;
        [ObservableProperty] private bool   abbWriteSeqNoEnabled = true;
        [ObservableProperty] private string aiApiKey            = "";

        partial void OnReaderPortChanged(string value)              => HasUnsavedChanges = true;
        partial void OnReaderBaudChanged(int value)                 => HasUnsavedChanges = true;
        partial void OnAbbSeqNoTaskChanged(string value)            => HasUnsavedChanges = true;
        partial void OnAbbSeqNoModuleChanged(string value)          => HasUnsavedChanges = true;
        partial void OnAbbSeqNoVariableChanged(string value)        => HasUnsavedChanges = true;
        partial void OnDeviceReconnectEnabledChanged(bool value)    => HasUnsavedChanges = true;
        partial void OnDeviceReconnectIntervalSecChanged(int value) => HasUnsavedChanges = true;
        partial void OnAbbWriteSeqNoEnabledChanged(bool value) => HasUnsavedChanges = true;
        partial void OnAiApiKeyChanged(string value)            => HasUnsavedChanges = true;

        // 日誌設定（直接映射 LogService，setter 同時觸發 HasUnsavedChanges）
        public bool LogSaveToFile
        {
            get => _core.Log.SaveToFile;
            set { _core.Log.SaveToFile = value; HasUnsavedChanges = true; OnPropertyChanged(); }
        }

        public int LogDaysToKeep
        {
            get => _core.Log.DaysToKeep;
            set { _core.Log.DaysToKeep = value; HasUnsavedChanges = true; OnPropertyChanged(); }
        }

        // 日誌上傳
        public ObservableCollection<string> AvailableLogFiles => _core.Log.AvailableLogFiles;
        public ObservableCollection<string> AttachmentPaths { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UploadLogCommand))]
        private string? selectedLogFile;

        [ObservableProperty] private string uploadStatus = "";
        [ObservableProperty] private bool? isUploadSuccess;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UploadLogCommand))]
        private bool isUploading = false;

        [ObservableProperty] private bool hasAttachments = false;

        public string AppVersion => typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        public ICommand ApplyCommand { get; }
        public IRelayCommand AddAttachmentCommand { get; }
        public IRelayCommand<string> RemoveAttachmentCommand { get; }
        public IAsyncRelayCommand UploadLogCommand { get; }

        public SystemSettingsViewModel(DashboardCoreServices core, ILogUploadService logUploadService,
            IConfigService<SystemConfigDto> systemConfig, IConfigService<HardwareConfigDto> hardwareConfig)
        {
            _core = core;
            _logUpload = logUploadService;
            _systemConfig = systemConfig;
            _hardwareConfig = hardwareConfig;
            LoadFromSettings();

            AttachmentPaths.CollectionChanged += (_, _) => HasAttachments = AttachmentPaths.Count > 0;

            SetLanguageCommand = new RelayCommand<string>(lang => {
                if (string.IsNullOrEmpty(lang)) return;
                Properties.Settings.Default.CultureCode = lang;
                Properties.Settings.Default.Save();
                ApplyLanguage(lang);
                OnPropertyChanged(nameof(CurrentLanguage));
            });
            SetThemeCommand = new RelayCommand<string>(isDarkStr => {
                var dark = isDarkStr == "True";
                Properties.Settings.Default.IsDarkMode = dark;
                Properties.Settings.Default.Save();
                ApplyTheme(dark);
                OnPropertyChanged(nameof(CurrentIsDark));
                OnPropertyChanged(nameof(CurrentIsLight));
            });

            ApplyCommand = new RelayCommand(Apply);
            AddAttachmentCommand = new RelayCommand(AddAttachment);
            RemoveAttachmentCommand = new RelayCommand<string>(path => AttachmentPaths.Remove(path!));
            UploadLogCommand = new AsyncRelayCommand(UploadLogAsync, () => !IsUploading && SelectedLogFile != null);

            _core.Log.RefreshAvailableLogFiles();
        }

        public void LoadFromSettings()
        {
            var sys = _systemConfig.Current;
            BusinessHour           = sys.BusinessHour;
            BusinessMinute         = sys.BusinessMinute;
            SyncEnabled            = sys.SyncEnabled;
            SyncIntervalSec        = sys.SyncIntervalSec;
            MissedCheckEnabled     = sys.MissedCheckEnabled;
            MissedCheckIntervalSec = sys.MissedCheckIntervalSec;
            IdleLogoutEnabled      = sys.IdleLogoutEnabled;
            IdleLogoutIntervalSec  = sys.IdleLogoutIntervalSec;
            LogSaveToFile          = sys.LogSaveToFile;
            LogDaysToKeep          = sys.LogDaysToKeep;
            SkipLoginScreen        = Properties.Settings.Default.SkipLoginScreen;

            var hw = _hardwareConfig.Current;
            ReaderPort                = hw.ReaderPort;
            ReaderBaud                = hw.ReaderBaud;
            AbbSeqNoTask              = hw.AbbSeqNoTask;
            AbbSeqNoModule            = hw.AbbSeqNoModule;
            AbbSeqNoVariable          = hw.AbbSeqNoVariable;
            DeviceReconnectEnabled    = hw.DeviceReconnectEnabled;
            DeviceReconnectIntervalSec = hw.DeviceReconnectIntervalSec;
            AbbWriteSeqNoEnabled       = hw.AbbWriteSeqNoEnabled;
            AiApiKey                   = sys.AiApiKey;

            HasUnsavedChanges = false;

            _core.Log.RefreshAvailableLogFiles();
        }

        private void Apply()
        {
            _systemConfig.Save(new SystemConfigDto
            {
                BusinessHour           = BusinessHour,
                BusinessMinute         = BusinessMinute,
                SyncEnabled            = SyncEnabled,
                SyncIntervalSec        = SyncIntervalSec,
                MissedCheckEnabled     = MissedCheckEnabled,
                MissedCheckIntervalSec = MissedCheckIntervalSec,
                IdleLogoutEnabled      = IdleLogoutEnabled,
                IdleLogoutIntervalSec  = IdleLogoutIntervalSec,
                LogSaveToFile          = LogSaveToFile,
                LogDaysToKeep          = LogDaysToKeep,
                AiApiKey               = AiApiKey,
            });
            _hardwareConfig.Save(new HardwareConfigDto
            {
                ReaderPort                = ReaderPort,
                ReaderBaud                = ReaderBaud,
                AbbSeqNoTask              = AbbSeqNoTask,
                AbbSeqNoModule            = AbbSeqNoModule,
                AbbSeqNoVariable          = AbbSeqNoVariable,
                DeviceReconnectEnabled    = DeviceReconnectEnabled,
                DeviceReconnectIntervalSec = DeviceReconnectIntervalSec,
                AbbWriteSeqNoEnabled       = AbbWriteSeqNoEnabled,
            });
            Properties.Settings.Default.SkipLoginScreen = SkipLoginScreen;
            Properties.Settings.Default.Save();
            HasUnsavedChanges = false;
            _core.Log.AddLog(Properties.Resources.MainProgressSuccess, LogLevel.Success);
        }

        private void AddAttachment()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Images & Documents|*.png;*.jpg;*.jpeg;*.bmp;*.xlsx;*.xls;*.pdf;*.txt|All Files|*.*",
                InitialDirectory = Directory.Exists(_core.Log.LogDirectory) ? _core.Log.LogDirectory : null
            };
            if (dialog.ShowDialog() != true) return;
            foreach (var path in dialog.FileNames)
                if (!AttachmentPaths.Contains(path))
                    AttachmentPaths.Add(path);
        }

        private async Task UploadLogAsync()
        {
            IsUploading = true;
            IsUploadSuccess = null;
            UploadStatus = "";
            try
            {
                var logPath = _core.Log.GetLogFilePath(SelectedLogFile!);
                var allPaths = new[] { logPath }
                    .Concat(AttachmentPaths)
                    .Select(p => Path.GetFullPath(p))
                    .ToList();

                var missing = allPaths.Where(p => !File.Exists(p)).ToList();
                if (missing.Any())
                    throw new FileNotFoundException(
                        $"附件不存在：{string.Join(", ", missing.Select(Path.GetFileName))}");

                using var ms = new MemoryStream();
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                    foreach (var p in allPaths)
                        zip.CreateEntryFromFile(p, Path.GetFileName(p));
                ms.Position = 0;

                var fileName = await _logUpload.UploadLogStreamAsync(ms, "report.zip");
                UploadStatus = $"已上傳：{fileName}";
                IsUploadSuccess = true;
                SelectedLogFile = null;
                AttachmentPaths.Clear();
                _core.Log.AddLog($"[UploadLog] 上傳成功：{fileName}", LogLevel.Success);
            }
            catch (Exception ex)
            {
                UploadStatus = ex.Message;
                IsUploadSuccess = false;
                _core.Log.AddErrorLog($"[UploadLogAsync] {ex.Message}");
            }
            finally
            {
                IsUploading = false;
            }
        }

        private void ApplyLanguage(string cultureCode)
        {
            var dict = new ResourceDictionary();
            dict.Source = new Uri($"Resources/StrResources.{cultureCode}.xaml", UriKind.Relative);

            var oldDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("StrResources"));

            if (oldDict != null)
            {
                int index = Application.Current.Resources.MergedDictionaries.IndexOf(oldDict);
                Application.Current.Resources.MergedDictionaries.RemoveAt(index);
                Application.Current.Resources.MergedDictionaries.Insert(index, dict);
            }
            else Application.Current.Resources.MergedDictionaries.Add(dict);

            var culture = new CultureInfo(cultureCode);
            Properties.Resources.Culture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        private void ApplyTheme(bool isDark)
        {
            var themeName = isDark ? "Dark" : "Light";
            var dictTheme = new ResourceDictionary();
            dictTheme.Source = new Uri($"Themes/Theme.{themeName}.xaml", UriKind.Relative);

            var oldDictTheme = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/Theme"));

            if (oldDictTheme != null)
            {
                int index = Application.Current.Resources.MergedDictionaries.IndexOf(oldDictTheme);
                Application.Current.Resources.MergedDictionaries.RemoveAt(index);
                Application.Current.Resources.MergedDictionaries.Insert(index, dictTheme);
            }
            else Application.Current.Resources.MergedDictionaries.Add(dictTheme);

            if (isDark) _paletteHelper.SetTheme(_darkTheme);
            else _paletteHelper.SetTheme(_lightTheme);
        }
    }
}
