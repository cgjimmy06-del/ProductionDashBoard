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
using System.Text.Json;
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
        private readonly ILicenseService _licenseService;

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

        // 授權資訊
        public string LicenseStatusText
        {
            get
            {
                var key = _licenseService.Status switch
                {
                    LicenseStatus.Valid             => "LicenseStatusValid",
                    LicenseStatus.ValidExpiringSoon => "LicenseStatusExpiringSoon",
                    LicenseStatus.Expired           => "LicenseStatusExpired",
                    LicenseStatus.InvalidSignature  => "LicenseStatusInvalid",
                    LicenseStatus.Trial             => "LicenseStatusTrial",
                    LicenseStatus.TrialExpiringSoon => "LicenseStatusExpiringSoon",
                    LicenseStatus.TrialExpired      => "LicenseStatusExpired",
                    LicenseStatus.Development       => "LicenseStatusDevelopment",
                    _                               => "LicenseStatusDevelopment",
                };
                return Application.Current.TryFindResource(key) as string ?? key;
            }
        }
        public string LicenseCustomerName    => _licenseService.CustomerName;
        public bool   HasLicenseCustomerName => !string.IsNullOrEmpty(_licenseService.CustomerName)
                                                && _licenseService.Status != LicenseStatus.Development;
        public string LicenseDaysRemainingText => _licenseService.DaysRemaining.ToString();
        public bool   HasLicenseDaysRemaining  => _licenseService.ExpiryDate.HasValue;
        public bool   IsChartsEnabled          => _licenseService.IsFeatureEnabled(LicensedFeature.Charts);
        public bool   IsSchedulingEnabled      => _licenseService.IsFeatureEnabled(LicensedFeature.Scheduling);
        public bool   IsProgramLibEnabled      => _licenseService.IsFeatureEnabled(LicensedFeature.ProgramLibrary);
        public bool   IsMaterialEnabled        => _licenseService.IsFeatureEnabled(LicensedFeature.MaterialManagement);

        public ICommand ApplyCommand { get; }
        public IRelayCommand AddAttachmentCommand { get; }
        public IRelayCommand<string> RemoveAttachmentCommand { get; }
        public IAsyncRelayCommand UploadLogCommand { get; }
        public ICommand ImportLicenseCommand { get; }

        public SystemSettingsViewModel(DashboardCoreServices core, ILogUploadService logUploadService,
            IConfigService<SystemConfigDto> systemConfig, IConfigService<HardwareConfigDto> hardwareConfig,
            ILicenseService licenseService)
        {
            _core = core;
            _logUpload = logUploadService;
            _systemConfig = systemConfig;
            _hardwareConfig = hardwareConfig;
            _licenseService = licenseService;
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
            ImportLicenseCommand = new AsyncRelayCommand(ImportLicenseAsync);

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

        private async Task ImportLicenseAsync()
        {
            var dialog = new OpenFileDialog { Filter = "授權檔 (*.lic)|*.lic" };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var json = await File.ReadAllTextAsync(dialog.FileName);
                var dto = JsonSerializer.Deserialize<Dtos.LicenseFileDto>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (dto == null) throw new InvalidOperationException("授權檔格式無效");

                JsonDataService.Save(dto, LicenseConstants.LicenseFileName);
                _licenseService.Reload();

                OnPropertyChanged(nameof(LicenseStatusText));
                OnPropertyChanged(nameof(LicenseCustomerName));
                OnPropertyChanged(nameof(HasLicenseCustomerName));
                OnPropertyChanged(nameof(LicenseDaysRemainingText));
                OnPropertyChanged(nameof(HasLicenseDaysRemaining));
                OnPropertyChanged(nameof(IsChartsEnabled));
                OnPropertyChanged(nameof(IsSchedulingEnabled));
                OnPropertyChanged(nameof(IsProgramLibEnabled));
                OnPropertyChanged(nameof(IsMaterialEnabled));

                _core.Log.AddLog("[SystemSettings] 授權已匯入，功能已更新", LogLevel.Success);
            }
            catch (Exception ex)
            {
                _core.Log.AddLog("[SystemSettings] 授權匯入失敗，請確認檔案格式", LogLevel.Error);
                _core.Log.AddErrorLog($"[ImportLicenseAsync] {ex.Message}");
            }
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
            var newSource = new Uri($"Themes/Theme.{(isDark ? "Dark" : "Light")}.xaml", UriKind.Relative);

            var existingThemeDict = Application.Current.Resources.MergedDictionaries.
                FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/Theme"));

            if (existingThemeDict != null)
                existingThemeDict.Source = newSource;
            else
                Application.Current.Resources.MergedDictionaries.Add(
                    new ResourceDictionary { Source = newSource });

            if (isDark) _paletteHelper.SetTheme(_darkTheme);
            else _paletteHelper.SetTheme(_lightTheme);
        }
    }
}
