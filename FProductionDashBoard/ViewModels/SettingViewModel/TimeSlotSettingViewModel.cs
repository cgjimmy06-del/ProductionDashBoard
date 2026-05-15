using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class TimeSlotSettingViewModel : SettingViewModelBase
    {
        public ObservableCollection<TimeSlotLookup> TimeSlotList { get; } = new();
        public List<int> HourItems { get; } = Enumerable.Range(0, 24).ToList();
        public List<int> MinuteItems { get; } = new() { 0, 30 };

        public int NextTimeSlotId { get; private set; } = 1;
        public bool IsNewMode { get; private set; } = true;

        [ObservableProperty] private int? editingId;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ComputedLabel))] private int formStartHour = 0;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ComputedLabel))] private int formStartMinute = 0;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ComputedLabel))] private int formEndHour = 0;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(ComputedLabel))] private int formEndMinute = 30;
        [ObservableProperty] private bool formIsCrossDay = false;

        public string ComputedLabel =>
            $"{FormStartHour:D2}:{FormStartMinute:D2}-{FormEndHour:D2}:{FormEndMinute:D2}";

        public TimeSlotSettingViewModel(DashboardCoreServices core, Services.IDialogService dialog)
            : base(core, dialog) { }

        protected override async Task LoadAsync()
        {
            try
            {
                var list = await _core.Data.GetTimeSlotsAsync();
                TimeSlotList.Clear();
                foreach (var s in list) TimeSlotList.Add(s);
                NextTimeSlotId = TimeSlotList.Count > 0 ? TimeSlotList.Max(s => s.TimeSlotId) + 1 : 1;
                OnPropertyChanged(nameof(NextTimeSlotId));
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"載入巡檢時段失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OpenNewForm()
        {
            EditingId = null;
            IsNewMode = true;
            OnPropertyChanged(nameof(IsNewMode));
            ClearForm();
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void Edit(TimeSlotLookup item)
        {
            EditingId = item.TimeSlotId;
            IsNewMode = false;
            OnPropertyChanged(nameof(IsNewMode));
            FormStartHour = item.StartAt.Hours;
            FormStartMinute = item.StartAt.Minutes;
            FormEndHour = item.EndAt.Hours;
            FormEndMinute = item.EndAt.Minutes;
            FormIsCrossDay = item.IsCrossDay;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private async Task Delete(TimeSlotLookup item)
        {
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseDelete}?"))
                return;
            try
            {
                await _core.Data.DeleteTimeSlotAsync(item.TimeSlotId);
                TimeSlotList.Remove(item);
                NextTimeSlotId = TimeSlotList.Count > 0 ? TimeSlotList.Max(s => s.TimeSlotId) + 1 : 1;
                OnPropertyChanged(nameof(NextTimeSlotId));
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"刪除巡檢時段失敗 (檢查是否有關聯紀錄): {ex.Message}", LogLevel.Error);
            }
        }

        protected override async Task SaveAsync()
        {
            var startTs = new TimeSpan(FormStartHour, FormStartMinute, 0);
            var endTs = new TimeSpan(FormEndHour, FormEndMinute, 0);

            if (!FormIsCrossDay && endTs <= startTs)
            {
                FormErrorString = "未勾選跨日時，結束時間必須大於開始時間";
                return;
            }

            try
            {
                var dto = new TimeSlotFormDto
                {
                    TimeSlotId = EditingId ?? NextTimeSlotId,
                    StartAt = startTs,
                    EndAt = endTs,
                    IsCrossDay = FormIsCrossDay,
                    Label = ComputedLabel
                };

                if (EditingId == null)
                    await _core.Data.AddTimeSlotAsync(dto);
                else
                    await _core.Data.UpdateTimeSlotAsync(dto);

                FormSuccessString = EditingId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _core.Log.AddLog($"儲存巡檢時段失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void ClearForm()
        {
            EditingId = null;
            FormStartHour = 0;
            FormStartMinute = 0;
            FormEndHour = 0;
            FormEndMinute = 30;
            FormIsCrossDay = false;
        }
    }
}
