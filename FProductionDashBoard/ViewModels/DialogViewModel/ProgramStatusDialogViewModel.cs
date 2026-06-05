using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace FProductionDashBoard.ViewModels
{
    public class ProgramStatusResult
    {
        public TuningType NewStatus { get; set; }
    }

    public partial class ProgramStatusDialogViewModel : DialogBaseViewModel<ProgramStatusResult>
    {
        public string PartNo { get; }
        public string Detail { get; }

        [ObservableProperty] private TuningType? selectedStatus;

        public Visibility TeachingVisibility   { get; }
        public Visibility OffsetVisibility     { get; }
        public Visibility FeasibleVisibility   { get; }
        public Visibility InfeasibleVisibility { get; }
        public Visibility PendingVisibility    { get; }

        public ICommand SelectTeachingCommand   { get; }
        public ICommand SelectOffsetCommand     { get; }
        public ICommand SelectFeasibleCommand   { get; }
        public ICommand SelectInfeasibleCommand { get; }
        public ICommand SelectPendingCommand    { get; }

        public ProgramStatusDialogViewModel(EquipmentProduct ep, string title) : base(title)
        {
            var part    = ep.Sop?.Product?.Part;
            var model   = ep.Sop?.Product?.Model;
            var process = ep.Sop?.Process;

            PartNo = part?.PartNo ?? "-";
            Detail = $"{part?.Brand} · {model?.Name} · {process?.Name}";

            var allowed = GetAllowedStatuses(ep.ProductionStatus);
            TeachingVisibility   = ToVisibility(allowed, TuningType.Teaching);
            OffsetVisibility     = ToVisibility(allowed, TuningType.Offset);
            FeasibleVisibility   = ToVisibility(allowed, TuningType.Feasible);
            InfeasibleVisibility = ToVisibility(allowed, TuningType.Infeasible);
            PendingVisibility    = ToVisibility(allowed, TuningType.Pending);

            SelectTeachingCommand   = new RelayCommand(() => SelectedStatus = TuningType.Teaching);
            SelectOffsetCommand     = new RelayCommand(() => SelectedStatus = TuningType.Offset);
            SelectFeasibleCommand   = new RelayCommand(() => SelectedStatus = TuningType.Feasible);
            SelectInfeasibleCommand = new RelayCommand(() => SelectedStatus = TuningType.Infeasible);
            SelectPendingCommand    = new RelayCommand(() => SelectedStatus = TuningType.Pending);
            ConfirmCommand = new RelayCommand(() => OnConfirm());
            CancelCommand  = new RelayCommand(() => OnCancel());
        }

        protected override void OnConfirm()
        {
            if (SelectedStatus == null)
            {
                DialogErrorString = Properties.Resources.ProgramStatusNoSelectionError;
                return;
            }
            Result = new ProgramStatusResult { NewStatus = SelectedStatus.Value };
            base.OnConfirm();
        }

        private static HashSet<TuningType> GetAllowedStatuses(TuningType current) => current switch
        {
            TuningType.Feasible   => [TuningType.Teaching, TuningType.Offset, TuningType.Infeasible, TuningType.Pending],
            TuningType.Pending    => [TuningType.Teaching, TuningType.Offset, TuningType.Feasible, TuningType.Infeasible],
            TuningType.Infeasible => [TuningType.Teaching, TuningType.Offset],
            TuningType.Teaching   => [TuningType.Offset, TuningType.Infeasible, TuningType.Pending],
            TuningType.Offset     => [TuningType.Teaching, TuningType.Infeasible, TuningType.Pending],
            _                     => []
        };

        private static Visibility ToVisibility(HashSet<TuningType> allowed, TuningType type)
            => allowed.Contains(type) ? Visibility.Visible : Visibility.Collapsed;
    }
}
