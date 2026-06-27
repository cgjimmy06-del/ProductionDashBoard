using FProductionDashBoard.ViewModels;
using System.Windows.Controls;

namespace FProductionDashBoard.UserControls.DialogView
{
    public partial class MesSyncDialog : UserControl
    {
        public MesSyncDialog()
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                if (DataContext is MesSyncDialogViewModel vm)
                    await vm.LoadCommand.ExecuteAsync(null);
            };
        }
    }
}
