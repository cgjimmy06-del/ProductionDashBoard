using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Services;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public partial class SystemSettingsViewModel : ObservableObject
    {
        private readonly DashboardCoreServices _core;

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

        partial void OnBusinessHourChanged(int value)           => HasUnsavedChanges = true;
        partial void OnBusinessMinuteChanged(int value)         => HasUnsavedChanges = true;
        partial void OnSyncEnabledChanged(bool value)           => HasUnsavedChanges = true;
        partial void OnSyncIntervalSecChanged(int value)        => HasUnsavedChanges = true;
        partial void OnMissedCheckEnabledChanged(bool value)    => HasUnsavedChanges = true;
        partial void OnMissedCheckIntervalSecChanged(int value) => HasUnsavedChanges = true;
        partial void OnIdleLogoutEnabledChanged(bool value)     => HasUnsavedChanges = true;
        partial void OnIdleLogoutIntervalSecChanged(int value)  => HasUnsavedChanges = true;

        public string AppVersion => typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        public ICommand ApplyCommand { get; }

        public SystemSettingsViewModel(DashboardCoreServices core)
        {
            _core = core;
            LoadFromSettings();

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
        }

        public void LoadFromSettings()
        {
            var s = Properties.Settings.Default;
            BusinessHour           = s.BusinessHour;
            BusinessMinute         = s.BusinessMinute;
            SyncEnabled            = s.SyncEnabled;
            SyncIntervalSec        = s.SyncIntervalSec;
            MissedCheckEnabled     = s.MissedCheckEnabled;
            MissedCheckIntervalSec = s.MissedCheckIntervalSec;
            IdleLogoutEnabled      = s.IdleLogoutEnabled;
            IdleLogoutIntervalSec  = s.IdleLogoutIntervalSec;
            HasUnsavedChanges      = false;
        }

        private void Apply()
        {
            var s = Properties.Settings.Default;
            s.BusinessHour           = BusinessHour;
            s.BusinessMinute         = BusinessMinute;
            s.SyncEnabled            = SyncEnabled;
            s.SyncIntervalSec        = SyncIntervalSec;
            s.MissedCheckEnabled     = MissedCheckEnabled;
            s.MissedCheckIntervalSec = MissedCheckIntervalSec;
            s.IdleLogoutEnabled      = IdleLogoutEnabled;
            s.IdleLogoutIntervalSec  = IdleLogoutIntervalSec;
            s.Save();
            HasUnsavedChanges = false;
            _core.Log.AddLog(Properties.Resources.MainProgressSuccess, LogLevel.Success);
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
                Application.Current.Resources.MergedDictionaries[index] = dict;
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
                Application.Current.Resources.MergedDictionaries[index] = dictTheme;
            }
            else Application.Current.Resources.MergedDictionaries.Add(dictTheme);

            if (isDark) _paletteHelper.SetTheme(_darkTheme);
            else _paletteHelper.SetTheme(_lightTheme);
        }
    }
}
