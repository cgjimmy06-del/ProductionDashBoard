using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FProductionDashBoard
{
    /// <summary>
    /// LoginWindow.xaml 的互動邏輯
    /// </summary>
    public partial class LoginWindow : Window
    {
        public string SelectedServer { get; set; } = "FS";
        public UserInfo? User { get; set; }

        public LoginWindow()
        {
            InitializeComponent();

            var vm = new LoginViewModel();
            if (Properties.Settings.Default.RememberMe)
            {
                vm.UserId = Properties.Settings.Default.Account;
                vm.SelectedServer = Properties.Settings.Default.Server;
                vm.RememberMe = true;
            }
            vm.OnLoginSuccess = (server, user) =>
            {
                SelectedServer = server;
                User = user;
                DialogResult = true;
            };
            DataContext = vm;
        }
    }
}
