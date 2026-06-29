using FProductionDashBoard.ViewModels;
using System.Windows.Controls;

namespace FProductionDashBoard.UserControls.DialogView
{
    public partial class MesSyncDialog : UserControl
    {
        public MesSyncDialog()
        {
            InitializeComponent();
            // InvokeCommandAction binding 在 DialogWindow 嵌套的 UserControl 中靜默失敗，需從 code-behind 直接呼叫
            Loaded += async (s, e) =>
            {
                if (DataContext is MesSyncDialogViewModel vm)
                    await vm.LoadCommand.ExecuteAsync(null);
            };
        }
    }
}
