using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Models;
using FProductionDashBoard.UiModels;
using System.Collections.Generic;

namespace FProductionDashBoard.ViewModels
{
    public record OrderAssignmentResult(int EquipmentId, int EquipmentProductId, int? Quantity, int? ScheduleId);

    public partial class OrderAssignmentDialogViewModel : DialogBaseViewModel<OrderAssignmentResult>
    {
        private readonly int _scheduleId;
        private readonly int _maxQty;

        public string PartNo { get; }
        public string ModelName { get; }
        public string ProcessName { get; }
        public string? LotNo { get; }
        public int ScheduleQty { get; }
        public string EquipmentName { get; }
        public string MaxQtyHint => string.Format(Properties.Resources.SchAssignQtyMaxHint, _maxQty);

        public IReadOnlyList<EquipmentProduct> CompatibleOptions { get; }

        [ObservableProperty] private EquipmentProduct? selectedEquipmentProduct;
        [ObservableProperty] private int? quantity;

        public OrderAssignmentDialogViewModel(
            ScheduleUiModel schedule,
            IReadOnlyList<EquipmentProduct> compatibleOptions,
            int remainingQty,
            string equipmentName)
            : base(Properties.Resources.SchAssignDialogTitle)
        {
            _scheduleId = schedule.ScheduleId;
            _maxQty = remainingQty;

            PartNo        = schedule.PartNo;
            ModelName     = schedule.ModelName;
            ProcessName   = schedule.ProcessName;
            LotNo         = schedule.LotNo;
            ScheduleQty   = schedule.Quantity;
            EquipmentName = equipmentName;

            CompatibleOptions        = compatibleOptions;
            SelectedEquipmentProduct = compatibleOptions.Count > 0 ? compatibleOptions[0] : null;
            Quantity                 = remainingQty > 0 ? remainingQty : (int?)null;
        }

        protected override void OnConfirm()
        {
            if (SelectedEquipmentProduct == null)
            {
                DialogErrorString = Properties.Resources.SchAssignNoSopSelected;
                return;
            }
            if (Quantity is null or <= 0)
            {
                DialogErrorString = Properties.Resources.SchAssignQtyRequired;
                return;
            }
            if (Quantity > _maxQty)
            {
                DialogErrorString = Properties.Resources.SchAssignQtyExceeded;
                return;
            }
            Result = new OrderAssignmentResult(
                SelectedEquipmentProduct.EquipmentId,
                SelectedEquipmentProduct.EquipmentProductId,
                Quantity,
                _scheduleId);
            base.OnConfirm();
        }
    }
}
