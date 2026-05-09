using FProductionDashBoard.ViewModels;

namespace FProductionDashBoard.Services
{
    public interface IDialogService
    {
        bool ShowConfirm(string message);
        TResult? ShowDialog<TResult>(DialogBaseViewModel<TResult> vm);
    }
}
