using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.Models;
using FProductionDashBoard.UiModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FProductionDashBoard.ViewModels
{
    public enum ScheduleOperationType { ForceComplete, Verify, Release, Split, Cancel }

    public class ScheduleOperationResult
    {
        public string? Description { get; set; }
        public int? ActualQuantity { get; set; }
        public int? RemainingQuantity { get; set; }
    }

    public partial class ScheduleOperationDialogViewModel : DialogBaseViewModel<ScheduleOperationResult>
    {
        public ScheduleOperationType OperationType { get; }

        // 對應接單清單（僅 Verify 使用）；D1：排除 Cancelled 後不得有 Pending/InProduction
        public IReadOnlyList<OrderProductionInfo> Orders { get; }
        public bool HasOrderList => OperationType == ScheduleOperationType.Verify && Orders.Count > 0;
        public bool AllOrdersCompleted => !Orders.Any(o =>
            o.Status == OrderProductionStatus.Pending ||
            o.Status == OrderProductionStatus.InProduction);
        public string? IncompleteHint
        {
            get
            {
                if (OperationType != ScheduleOperationType.Verify || AllOrdersCompleted)
                    return null;
                var names = Orders
                    .Where(o => o.Status == OrderProductionStatus.Pending
                             || o.Status == OrderProductionStatus.InProduction)
                    .Select(o => o.EquipmentName)
                    .Where(n => !string.IsNullOrWhiteSpace(n));
                return string.Format(Properties.Resources.PioVerifyIncompleteHint, string.Join("、", names));
            }
        }

        public bool IsDescriptionVisible => true;
        public bool IsActualQtyVisible   => OperationType == ScheduleOperationType.ForceComplete
                                         || OperationType == ScheduleOperationType.Verify;
        public bool IsRemainingQtyVisible => OperationType == ScheduleOperationType.Split;
        public bool IsDescriptionRequired => OperationType == ScheduleOperationType.ForceComplete;

        [ObservableProperty] private string description = string.Empty;
        [ObservableProperty] private int? actualQuantity;
        [ObservableProperty] private int? remainingQuantity;

        private int _maxRemainingQty;
        public string MaxRemainingQtyHint =>
            string.Format(Properties.Resources.ScheduleOpRemainingQtyHint, _maxRemainingQty);

        public ScheduleOperationDialogViewModel(
            ScheduleOperationType operationType,
            ScheduleUiModel schedule,
            string currentUserName,
            IReadOnlyList<OrderProductionInfo>? orders = null)
            : base(GetTitle(operationType))
        {
            OperationType = operationType;
            Orders = orders ?? Array.Empty<OrderProductionInfo>();

            if (operationType == ScheduleOperationType.ForceComplete)
                Description = $"{Properties.Resources.ScheduleOpForcedByPrefix}{currentUserName}{Properties.Resources.ScheduleOpForcedBySuffix}";

            if (IsActualQtyVisible)
                ActualQuantity = schedule.ActualQuantity ?? schedule.Quantity;

            if (IsRemainingQtyVisible)
            { 
                _maxRemainingQty = schedule.Quantity - (schedule.ActualQuantity ?? 0);
                RemainingQuantity = _maxRemainingQty;
            }
        }

        protected override void OnConfirm()
        {
            if (OperationType == ScheduleOperationType.Verify && !AllOrdersCompleted)
            {
                DialogErrorString = IncompleteHint;
                return;
            }
            if (IsDescriptionRequired && string.IsNullOrWhiteSpace(Description))
            {
                DialogErrorString = Properties.Resources.ScheduleOpDescRequired;
                return;
            }
            if (IsRemainingQtyVisible && (RemainingQuantity == null || RemainingQuantity <= 0))
            {
                DialogErrorString = Properties.Resources.ScheduleOpRemainingQtyRequired;
                return;
            }
            if (IsRemainingQtyVisible && RemainingQuantity > _maxRemainingQty)
            {
                DialogErrorString = Properties.Resources.ScheduleOpRemainingQtyExceeded;
                return;
            }
            Result = new ScheduleOperationResult
            {
                Description       = string.IsNullOrWhiteSpace(Description) ? null : Description,
                ActualQuantity    = IsActualQtyVisible ? ActualQuantity : null,
                RemainingQuantity = IsRemainingQtyVisible ? RemainingQuantity : null
            };
            base.OnConfirm();
        }

        private static string GetTitle(ScheduleOperationType type) => type switch
        {
            ScheduleOperationType.ForceComplete => Properties.Resources.ScheduleOpForceCompleteTitle,
            ScheduleOperationType.Verify        => Properties.Resources.ScheduleOpVerifyTitle,
            ScheduleOperationType.Release       => Properties.Resources.ScheduleOpReleaseTitle,
            ScheduleOperationType.Split         => Properties.Resources.ScheduleOpSplitTitle,
            ScheduleOperationType.Cancel        => Properties.Resources.ScheduleOpCancelTitle,
            _                                   => string.Empty
        };
    }
}
