using CommunityToolkit.Mvvm.ComponentModel;
using FProductionDashBoard.UiModels;
using System.Collections.Generic;

namespace FProductionDashBoard.ViewModels
{
    /// <summary>倉位指派/改倉 Dialog 結果：LocationId 為 null 代表選了「(無)」＝取消指派。</summary>
    public record AssignLocationResult(int? LocationId);

    public partial class AssignLocationDialogViewModel : DialogBaseViewModel<AssignLocationResult>
    {
        public int ScheduleId { get; }
        public string PartNo { get; }
        public string ModelName { get; }
        public string ProcessName { get; }
        public string? CurrentLocationCode { get; }

        /// <summary>可選倉位（含首項「(無)」）。與入料表單共用同一份 <see cref="PioLocationChoice"/> 清單。</summary>
        public IReadOnlyList<PioLocationChoice> Choices { get; }

        [ObservableProperty] private int? selectedLocationId;

        public AssignLocationDialogViewModel(ScheduleUiModel schedule, IReadOnlyList<PioLocationChoice> choices)
            : base(Properties.Resources.AssignLocationDialogTitle)
        {
            ScheduleId          = schedule.ScheduleId;
            PartNo              = schedule.PartNo;
            ModelName           = schedule.ModelName;
            ProcessName         = schedule.ProcessName;
            CurrentLocationCode = schedule.LocationCode;

            Choices            = choices;
            SelectedLocationId = schedule.LocationId;   // 預選目前倉位；未指派→null→預選「(無)」
        }

        protected override void OnConfirm()
        {
            // 「(無)」恆為可選項且開窗時已預選目前狀態，永遠有有效選取，故無「未選」錯誤
            Result = new AssignLocationResult(SelectedLocationId);
            base.OnConfirm();
        }
    }
}
