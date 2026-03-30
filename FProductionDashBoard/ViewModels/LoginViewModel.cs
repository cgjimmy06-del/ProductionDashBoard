using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Repositories;
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

namespace FProductionDashBoard.ViewModels
{
    public class LanguageOption
    {
        public string DisplayName { get; set; } = "";   // 顯示在 ComboBox 的文字
        public string CultureCode { get; set; } = "";   // 用來切換語言的代碼
    }

    public partial class LoginViewModel : ObservableObject
    {
        public string AppVersion { get; }

        // 樣式主題
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();
        private readonly Theme _lightTheme;
        private readonly Theme _darkTheme;

        public ObservableCollection<string> Servers { get; } =
        new ObservableCollection<string> { "FS", "GS", "VS" };
        public ObservableCollection<LanguageOption> Languages { get; } =
        new ObservableCollection<LanguageOption> { 
            new LanguageOption { DisplayName = "中文", CultureCode = "zh-TW" },
            new LanguageOption { DisplayName = "English", CultureCode = "en-US" },
            new LanguageOption { DisplayName = "Tiếng Việt", CultureCode = "vi-VN" }};

        [ObservableProperty]
        private string selectedTheme = "Light";
        [ObservableProperty]
        private string selectedLanguage = "zh-TW";
        [ObservableProperty]
        private string selectedServer = "FS";
        [ObservableProperty]
        private string userId = "";
        [ObservableProperty]
        private string password = "";
        [ObservableProperty]
        private bool rememberMe = false;

        [ObservableProperty]
        private string errorinfo = "";

        public ICommand LoginCommand { get; }
        public ICommand LanguageChangeCommand { get; }
        public ICommand ThemeChangeCommand { get; }
        public Action<string, Models.UserInfo>? OnLoginSuccess { get; set; }

        public LoginViewModel()
        {
            // 讀取 FileVersion
            AppVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion ?? "Unknown";

            // RelayCommand<T> 可以直接接收參數型別
            LanguageChangeCommand = new RelayCommand(() => languageChange());
            ThemeChangeCommand = new RelayCommand(() => themeChange());
            LoginCommand = new RelayCommand<string>(param =>
            {
                param ??= "";
                if (param == "Normal") // 正常登入
                { }
                else if (param == "Visitor") // 訪客登入 (後續刷卡擴充)
                {
                    UserId = "visitor";
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
            if (UserId == "" || Password == "")
            { Errorinfo = Properties.Resources.LogInFillOutError; return; }

            if (!LoginService.checkConnection(SelectedServer))
            { Errorinfo = Properties.Resources.LogInConnectionError; return; }

            var user = LoginService.validateUser(SelectedServer, UserId, Password);
            if (user != null)
            {
                if (loginSource != "Normal") { UserId = ""; Password = ""; } // 避免記憶 非常規登入資訊

                saveDefault();
                OnLoginSuccess?.Invoke(SelectedServer, user);
            }
            else { Errorinfo = Properties.Resources.LogInAccountError; }
        }
        private void saveDefault() // 儲存預設
        {
            Properties.Settings.Default.RememberMe = RememberMe;
            Properties.Settings.Default.Theme = SelectedTheme;
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

            if (SelectedTheme != Properties.Settings.Default.Theme &&
                Properties.Settings.Default.Theme != string.Empty)
            { SelectedTheme = Properties.Settings.Default.Theme; themeChange(); }

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
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(SelectedLanguage);
            Thread.CurrentThread.CurrentCulture = new CultureInfo(SelectedLanguage);
        }
        private void themeChange() // 主題切換
        {
            // 切換 ResourceDictionary 主題
            var dictTheme = new ResourceDictionary();

            dictTheme.Source = new Uri($"Themes/Theme.{SelectedTheme}.xaml", UriKind.Relative);

            var oldDictTheme = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/Theme"));

            if (oldDictTheme != null)
            {
                int index = Application.Current.Resources.MergedDictionaries.IndexOf(oldDictTheme);
                Application.Current.Resources.MergedDictionaries[index] = dictTheme;
            } else Application.Current.Resources.MergedDictionaries.Add(dictTheme);

            //// 切換 Material Design 主題
            if (SelectedTheme == "Dark") _paletteHelper.SetTheme(_darkTheme);
            else _paletteHelper.SetTheme(_lightTheme);
        }
    }
}
