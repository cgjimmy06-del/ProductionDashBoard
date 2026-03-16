using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FProductionDashBoard
{
    public partial class LoginViewModel : ObservableObject
    {
        public string AppVersion { get; }

        public ObservableCollection<string> Servers { get; } =
        new ObservableCollection<string> { "FS", "GS", "VS" };

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
        public Action<string, UserInfo>? OnLoginSuccess { get; set; }

        public LoginViewModel()
        {
            // 讀取 FileVersion
            AppVersion = FileVersionInfo.GetVersionInfo(
                Assembly.GetExecutingAssembly().Location).FileVersion ?? "Unknown";

            // RelayCommand<T> 可以直接接收參數型別
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

                if (LoginService.checkConnection(SelectedServer))
                    Errorinfo = "error: Check the connection!";

                var user = LoginService.validateUser(SelectedServer, UserId, Password);
                if (user != null)
                {
                    if (RememberMe)
                    {
                        Properties.Settings.Default.Account = UserId;
                        Properties.Settings.Default.Server = SelectedServer;
                        Properties.Settings.Default.RememberMe = true;
                    }
                    else
                    {
                        Properties.Settings.Default.Account = string.Empty;
                        Properties.Settings.Default.Server = SelectedServer;
                        Properties.Settings.Default.RememberMe = false;
                    }
                    Properties.Settings.Default.Save();

                    OnLoginSuccess?.Invoke(SelectedServer, user);
                }
                else Errorinfo = "error: Check account or password!";



            });
        }



    }
}
