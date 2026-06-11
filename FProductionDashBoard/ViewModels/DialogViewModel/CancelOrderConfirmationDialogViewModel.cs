using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.UiModels;

namespace FProductionDashBoard.ViewModels
{
    public record CancelOrderResult(string? Description);

    public partial class CancelOrderConfirmationDialogViewModel : DialogBaseViewModel<CancelOrderResult>
    {
        public int OrderId { get; }
        public string ProductName { get; }
        public string ProcessName { get; }
        public int? Quantity { get; }

        [ObservableProperty] private string? description;

        public CancelOrderConfirmationDialogViewModel(OrderProductionInfo order)
            : base(Properties.Resources.SchCancelOrderDialogTitle)
        {
            OrderId     = order.OrderId;
            ProductName = order.ProductName;
            ProcessName = order.ProcessName;
            Quantity    = order.Quantity;
        }

        protected override void OnConfirm()
        {
            Result = new CancelOrderResult(Description);
            base.OnConfirm();
        }
    }
}
