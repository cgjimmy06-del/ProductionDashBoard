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
        [ObservableProperty] private int formStartHour = 0;
        [ObservableProperty] private int formStartMinute = 0;
        [ObservableProperty] private int formEndHour = 0;
        [ObservableProperty] private int formEndMinute = 30;
        [ObservableProperty] private bool formIsCrossDay = false;

        public string ComputedLabel =>
            $"{FormStartHour:D2}:{FormStartMinute:D2}-{FormEndHour:D2}:{FormEndMinute:D2}";

        public TimeSlotSettingViewModel(LogService log, IDataService dataService)
            : base(log, dataService) { }

        partial void OnFormStartHourChanged(int value) => OnPropertyChanged(nameof(ComputedLabel));
        partial void OnFormStartMinuteChanged(int value) => OnPropertyChanged(nameof(ComputedLabel));
        partial void OnFormEndHourChanged(int value) => OnPropertyChanged(nameof(ComputedLabel));
        partial void OnFormEndMinuteChanged(int value) => OnPropertyChanged(nameof(ComputedLabel));

        protected override async Task LoadAsync()
        {
            try
            {
                var list = await _dataService.GetTimeSlotsAsync();
                TimeSlotList.Clear();
                foreach (var s in list) TimeSlotList.Add(s);
                NextTimeSlotId = TimeSlotList.Count > 0 ? TimeSlotList.Max(s => s.TimeSlotId) + 1 : 1;
                OnPropertyChanged(nameof(NextTimeSlotId));
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _log.AddLog($"載入巡檢時段失敗: {ex.Message}", LogLevel.Error);
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
                await _dataService.DeleteTimeSlotAsync(item.TimeSlotId);
                TimeSlotList.Remove(item);
                NextTimeSlotId = TimeSlotList.Count > 0 ? TimeSlotList.Max(s => s.TimeSlotId) + 1 : 1;
                OnPropertyChanged(nameof(NextTimeSlotId));
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _log.AddLog($"刪除巡檢時段失敗 (檢查是否有關聯紀錄): {ex.Message}", LogLevel.Error);
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
                    await _dataService.AddTimeSlotAsync(dto);
                else
                    await _dataService.UpdateTimeSlotAsync(dto);

                FormSuccessString = EditingId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _log.AddLog($"儲存巡檢時段失敗: {ex.Message}", LogLevel.Error);
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
