using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.UiModels;

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

        public bool IsDescriptionVisible => true;
        public bool IsActualQtyVisible   => OperationType == ScheduleOperationType.ForceComplete
                                         || OperationType == ScheduleOperationType.Verify;
        public bool IsRemainingQtyVisible => OperationType == ScheduleOperationType.Split;
        public bool IsDescriptionRequired => OperationType == ScheduleOperationType.ForceComplete;

        [ObservableProperty] private string description = string.Empty;
        [ObservableProperty] private int? actualQuantity;
        [ObservableProperty] private int? remainingQuantity;

        public ScheduleOperationDialogViewModel(
            ScheduleOperationType operationType,
            ScheduleUiModel schedule,
            string currentUserName)
            : base(GetTitle(operationType))
        {
            OperationType = operationType;

            if (operationType == ScheduleOperationType.ForceComplete)
                Description = $"{Properties.Resources.ScheduleOpForcedByPrefix}{currentUserName}{Properties.Resources.ScheduleOpForcedBySuffix}";

            if (IsActualQtyVisible)
                ActualQuantity = schedule.Quantity;
        }

        protected override void OnConfirm()
        {
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
