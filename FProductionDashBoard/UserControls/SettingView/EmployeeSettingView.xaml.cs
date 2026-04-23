using FProductionDashBoard.ViewModels;
using System.Windows.Controls;

namespace FProductionDashBoard.UserControls
{
    public partial class EmployeeSettingView : UserControl
    {
        public EmployeeSettingView()
        {
            InitializeComponent();
        }

        private void PwdBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is EmployeeSettingViewModel vm)
                vm.FormPassword = PwdBox.Password;
        }
    }
}
