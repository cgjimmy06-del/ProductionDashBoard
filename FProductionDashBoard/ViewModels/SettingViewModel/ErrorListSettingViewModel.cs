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
    public partial class ErrorListSettingViewModel : SettingViewModelBase
    {
        public ObservableCollection<ErrorList> ErrorItems { get; } = new();
        public List<int> SeverityItems { get; } = new() { 0, 1, 2 };
        public ObservableCollection<ListType> ListTypes { get; } = new();

        [ObservableProperty] private int? editingId;
        [ObservableProperty] private bool isNewMode = true;
        [ObservableProperty] private string formErrorCode = "";
        [ObservableProperty] private int? formTypeId = 1;
        [ObservableProperty] private int formSeverity = 0;
        [ObservableProperty] private string? formMessageZhTw;
        [ObservableProperty] private string? formMessageEnUs;
        [ObservableProperty] private string? formMessageViVn;

        public ErrorListSettingViewModel(DashboardCoreServices core)
            : base(core) { }

        protected override async Task LoadAsync()
        {
            try
            {
                if (!ListTypes.Any())
                {
                    var types = await _core.Data.GetListTypesAsync();
                    foreach (var t in types) ListTypes.Add(t);
                }
                var list = await _core.Data.GetAllErrorListsAsync();
                ErrorItems.Clear();
                foreach (var e in list) ErrorItems.Add(e);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"載入錯誤清單失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OpenNewForm()
        {
            EditingId = null;
            IsNewMode = true;
            ClearForm();
            SuggestErrorCode();
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        partial void OnFormTypeIdChanged(int? value) => SuggestErrorCode();

        private void SuggestErrorCode()
        {
            if (!IsNewMode) return;
            var type = ListTypes.FirstOrDefault(t => t.TypeId == FormTypeId);
            if (type == null) return;

            string prefix = type.Name;
            int digitLen = 8 - prefix.Length;
            if (digitLen <= 0) return;

            int maxNum = ErrorItems
                .Where(e => e.ErrorCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(e =>
                {
                    var suffix = e.ErrorCode[prefix.Length..];
                    return int.TryParse(suffix, out int n) ? n : 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            FormErrorCode = prefix + (maxNum + 1).ToString().PadLeft(digitLen, '0');
        }

        [RelayCommand]
        private void Edit(ErrorList item)
        {
            EditingId = item.ErrorId;
            IsNewMode = false;
            FormErrorCode = item.ErrorCode;
            FormTypeId = item.TypeId;
            FormSeverity = item.Severity;
            FormMessageZhTw = item.Translations.FirstOrDefault(t => t.LanguageCode == "zh-TW")?.Message;
            FormMessageEnUs = item.Translations.FirstOrDefault(t => t.LanguageCode == "en-US")?.Message;
            FormMessageViVn = item.Translations.FirstOrDefault(t => t.LanguageCode == "vi-VN")?.Message;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private async Task Delete(ErrorList item)
        {
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseDelete}?"))
                return;
            try
            {
                await _core.Data.DeleteErrorListAsync(item.ErrorId);
                ErrorItems.Remove(item);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"刪除錯誤代碼失敗 (檢查是否有關聯紀錄): {ex.Message}", LogLevel.Error);
            }
        }

        protected override async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormErrorCode))
            { FormErrorString = "ErrorCode 為必填"; return; }

            try
            {
                var dto = new ErrorListFormDto
                {
                    Id = EditingId,
                    ErrorCode = FormErrorCode,
                    TypeId = FormTypeId,
                    Severity = FormSeverity,
                    MessageZhTw = FormMessageZhTw,
                    MessageEnUs = FormMessageEnUs,
                    MessageViVn = FormMessageViVn
                };

                if (EditingId == null)
                    await _core.Data.AddErrorListAsync(dto);
                else
                    await _core.Data.UpdateErrorListAsync(dto);

                FormSuccessString = EditingId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _core.Log.AddLog($"儲存錯誤代碼失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void ClearForm()
        {
            EditingId = null;
            IsNewMode = true;
            FormErrorCode = "";
            FormTypeId = 1;
            FormSeverity = 0;
            FormMessageZhTw = null;
            FormMessageEnUs = null;
            FormMessageViVn = null;
        }
    }
}
