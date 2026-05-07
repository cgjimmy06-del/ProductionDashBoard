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
        public string SelectedServer { get; set; } = "";
        public UiModels.UserInfo User { get; set; } = new UiModels.UserInfo() { UserId = "unknown", Name = "unknown" };

        public LoginWindow()
        {
            InitializeComponent();

            var vm = new ViewModels.LoginViewModel();
            DataContext = vm;
            // 登入成功後事件
            vm.OnLoginSuccess = (server, user) =>
            {
                SelectedServer = server;
                User = user;
                DialogResult = true;
            };
            vm.loadDefault(); //載入預設
        }
    }
}
