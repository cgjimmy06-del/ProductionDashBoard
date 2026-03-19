using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Repositories;
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

namespace FProductionDashBoard
{
    public class LanguageOption
    {
        public string DisplayName { get; set; } = "";   // 顯示在 ComboBox 的文字
        public string CultureCode { get; set; } = "";   // 用來切換語言的代碼
    }

    public partial class LoginViewModel : ObservableObject
    {
        public string AppVersion { get; }

        public ObservableCollection<string> Servers { get; } =
        new ObservableCollection<string> { "FS", "GS", "VS" };
        public ObservableCollection<LanguageOption> Languages { get; } =
        new ObservableCollection<LanguageOption> { 
            new LanguageOption { DisplayName = "中文", CultureCode = "zh-TW" },
            new LanguageOption { DisplayName = "English", CultureCode = "en-US" },
            new LanguageOption { DisplayName = "Tiếng Việt", CultureCode = "vi-VN" }};

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
        public Action<string, UserInfo>? OnLoginSuccess { get; set; }

        public LoginViewModel()
        {
            // 讀取 FileVersion
            AppVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion ?? "Unknown";

            // RelayCommand<T> 可以直接接收參數型別
            LanguageChangeCommand = new RelayCommand(() => languageChange());
            LoginCommand = new RelayCommand<string>(param =>
            {
                param ??= "";
                if (param == "Normal") // 正常登入
                { }
                else if (param == "Visitor") // 訪客登入 (後續刷卡擴充)
                {
                    UserId = "F0000000";
                    Password = "0000";
                }

                logInEvent();

                if (param != "Normal") { UserId = ""; Password = ""; } // 非正常登入則清空
            });
        }

        private void logInEvent()
        {
            if (UserId == "" || Password == "")
            { Errorinfo = Properties.Resources.LogInFillOutError; return; }

            if (!LoginService.checkConnection(SelectedServer))
            { Errorinfo = Properties.Resources.LogInConnectionError; return; }

            var user = LoginService.validateUser(SelectedServer, UserId, Password);
            if (user != null)
            {
                saveDefault();
                OnLoginSuccess?.Invoke(SelectedServer, user);
            }
            else { Errorinfo = Properties.Resources.LogInAccountError; }
        }
        private void saveDefault() // 儲存預設
        {
            if (RememberMe)
            {
                Properties.Settings.Default.Account = UserId;
                Properties.Settings.Default.CultureCode = SelectedLanguage;
                Properties.Settings.Default.Server = SelectedServer;
                Properties.Settings.Default.RememberMe = true;
            }
            else
            {
                Properties.Settings.Default.Account = string.Empty;
                Properties.Settings.Default.CultureCode = SelectedLanguage;
                Properties.Settings.Default.Server = SelectedServer;
                Properties.Settings.Default.RememberMe = false;
            }
            Properties.Settings.Default.Save();
        }
        public void languageChange() // 語言切換
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

    }
}
