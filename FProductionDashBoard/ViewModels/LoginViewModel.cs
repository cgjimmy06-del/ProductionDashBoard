using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.Services;

namespace FProductionDashBoard.ViewModels
{
    public class LanguageOption
    {
        public string DisplayName { get; set; } = "";   // 顯示在 ComboBox 的文字
        public string CultureCode { get; set; } = "";   // 用來切換語言的代碼
    }

    public partial class LoginViewModel : ObservableObject
    {
        public string AppVersion => typeof(App).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        private readonly UiModels.UserInfo _visitorLogin =
            new UiModels.UserInfo { UserId = "visitor", Name = "訪客", RoleId = 1, Id = 2 };

        // 樣式主題
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();
        private readonly Theme _lightTheme;
        private readonly Theme _darkTheme;

        public ObservableCollection<string> Servers { get; } =
        new ObservableCollection<string> { "TT" }; // { "FS", "GS", "VS" }; { "TT" };
        public ObservableCollection<LanguageOption> Languages { get; } =
        new ObservableCollection<LanguageOption> { 
            new LanguageOption { DisplayName = "中文", CultureCode = "zh-TW" },
            new LanguageOption { DisplayName = "English", CultureCode = "en-US" },
            new LanguageOption { DisplayName = "Tiếng Việt", CultureCode = "vi-VN" }};

        [ObservableProperty]
        private string selectedLanguage = "zh-TW";
        [ObservableProperty]
        private string selectedServer = "";
        [ObservableProperty]
        private bool isDarkMode = false;
        [ObservableProperty]
        private string userId = "";
        [ObservableProperty]
        private string password = "";
        [ObservableProperty]
        private bool rememberMe = false;

        [ObservableProperty]
        private string errorinfo = "";

        public bool IsSettingsEnabled { get; }

        public ICommand LoginCommand { get; }
        public ICommand LanguageChangeCommand { get; }
        public ICommand ThemeChangeCommand { get; }
        public Action<string, UserInfo>? OnLoginSuccess { get; set; }

        public LoginViewModel(bool isSettingsEnabled = true)
        {
            IsSettingsEnabled = isSettingsEnabled;
            // RelayCommand<T> 可以直接接收參數型別
            LanguageChangeCommand = new RelayCommand(() => languageChange());
            ThemeChangeCommand = new RelayCommand(() => themeChange());
            LoginCommand = new RelayCommand<string>(param =>
            {
                param ??= "";
                if (param == "Normal") // 正常登入
                { }
                else if (param == "Visitor") // 訪客登入
                {
                    UserId = _visitorLogin.UserId;
                    Password = "0000";
                }
                logInEvent(param);
            });

            // 主題顏色設定
            _lightTheme = Theme.Create(BaseTheme.Light,
               SwatchHelper.Lookup[MaterialDesignColor.Indigo], SwatchHelper.Lookup[MaterialDesignColor.Lime]);
            _darkTheme = Theme.Create(BaseTheme.Dark,
               SwatchHelper.Lookup[MaterialDesignColor.Indigo], SwatchHelper.Lookup[MaterialDesignColor.Lime]);
            _paletteHelper.SetTheme(_lightTheme);
        }

        private void logInEvent(string loginSource)
        {
            if (UserId == "" || Password == "" || SelectedServer == "")
            { Errorinfo = Properties.Resources.LogInFillOutError; return; }

            UserInfo? user = _visitorLogin; // 預設 loginSource = "Visitor"

            if (loginSource == "Normal") // 只有常規登入要經過資料庫驗證
            {
                if (!LoginDapper.checkConnection(SelectedServer))
                { Errorinfo = Properties.Resources.LogInConnectionError; return; }
                // 之後password透過EncryptionService加密後儲存
                user = LoginDapper.validateUser(SelectedServer, UserId, Password);
            }

            if (user != null)
            {
                if (loginSource != "Normal") { UserId = ""; Password = ""; } // 避免記憶 非常規登入資訊

                saveDefault();
                OnLoginSuccess?.Invoke(SelectedServer, user); // 事件於LoginWindow的Code-behind
            }
            else { Errorinfo = Properties.Resources.LogInAccountError; }
        }
        private void saveDefault() // 儲存預設
        {
            Properties.Settings.Default.RememberMe = RememberMe;
            Properties.Settings.Default.IsDarkMode = IsDarkMode;
            Properties.Settings.Default.CultureCode = SelectedLanguage;
            Properties.Settings.Default.Server = SelectedServer;

            if (RememberMe)
            {
                Properties.Settings.Default.Account = UserId;
                Properties.Settings.Default.Password = Password;
            }
            else
            {
                Properties.Settings.Default.Account = string.Empty;
                Properties.Settings.Default.Password = string.Empty;
            }
            Properties.Settings.Default.Save();
        }
        public void loadDefault() // 載入預設
        {
            RememberMe = Properties.Settings.Default.RememberMe;
            SelectedServer = Properties.Settings.Default.Server;
            UserId = Properties.Settings.Default.Account;
            Password = Properties.Settings.Default.Password;

            if (IsDarkMode != Properties.Settings.Default.IsDarkMode)
            { IsDarkMode = Properties.Settings.Default.IsDarkMode; themeChange(); }

            if (SelectedLanguage != Properties.Settings.Default.CultureCode && 
                Properties.Settings.Default.CultureCode != string.Empty)
            { SelectedLanguage = Properties.Settings.Default.CultureCode; languageChange(); }
        }
        private void languageChange() // 語言切換
        {
            // 切換 ResourceDictionary (UI字串)
            var dict = new ResourceDictionary();
            dict.Source = new Uri($"Resources/StrResources.{SelectedLanguage}.xaml", UriKind.Relative);

            var oldDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("StrResources"));

            if (oldDict != null)
            {
                int index = Application.Current.Resources.MergedDictionaries.IndexOf(oldDict);
                Application.Current.Resources.MergedDictionaries[index] = dict;
            } else Application.Current.Resources.MergedDictionaries.Add(dict);

            // 切換 .resx (後端訊息)
            var currentCulture = new CultureInfo(SelectedLanguage);
            Properties.Resources.Culture = currentCulture;
            CultureInfo.DefaultThreadCurrentUICulture = currentCulture;
            Thread.CurrentThread.CurrentUICulture = currentCulture;
        }
        private void themeChange() // 主題切換
        {
            var themeName = IsDarkMode ? "Dark" : "Light";

            // 切換 ResourceDictionary 主題
            var dictTheme = new ResourceDictionary();
            dictTheme.Source = new Uri($"Themes/Theme.{themeName}.xaml", UriKind.Relative);

            var oldDictTheme = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/Theme"));

            if (oldDictTheme != null)
            {
                int index = Application.Current.Resources.MergedDictionaries.IndexOf(oldDictTheme);
                Application.Current.Resources.MergedDictionaries[index] = dictTheme;
            } else Application.Current.Resources.MergedDictionaries.Add(dictTheme);

            //// 切換 Material Design 主題
            if (IsDarkMode) _paletteHelper.SetTheme(_darkTheme);
            else _paletteHelper.SetTheme(_lightTheme);
        }
    }
}
