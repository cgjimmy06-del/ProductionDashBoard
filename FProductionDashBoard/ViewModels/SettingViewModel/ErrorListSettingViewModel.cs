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

        [ObservableProperty] private int? editingId;
        [ObservableProperty] private bool isNewMode = true;
        [ObservableProperty] private string formErrorCode = "";
        [ObservableProperty] private string? formCategory;
        [ObservableProperty] private int formSeverity = 0;
        [ObservableProperty] private string? formMessageZhTw;
        [ObservableProperty] private string? formMessageEnUs;
        [ObservableProperty] private string? formMessageViVn;

        public ErrorListSettingViewModel(LogService log, IDataService dataService)
            : base(log, dataService) { }

        protected override async Task LoadAsync()
        {
            try
            {
                var list = await _dataService.GetAllErrorListsAsync();
                ErrorItems.Clear();
                foreach (var e in list) ErrorItems.Add(e);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _log.AddLog($"載入錯誤清單失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OpenNewForm()
        {
            EditingId = null;
            IsNewMode = true;
            ClearForm();
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void Edit(ErrorList item)
        {
            EditingId = item.ErrorId;
            IsNewMode = false;
            FormErrorCode = item.ErrorCode;
            FormCategory = item.Category;
            FormSeverity = item.Severity ?? 0;
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
            try
            {
                await _dataService.DeleteErrorListAsync(item.ErrorId);
                ErrorItems.Remove(item);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _log.AddLog($"刪除錯誤代碼失敗: {ex.Message}", LogLevel.Error);
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
                    Category = FormCategory,
                    Severity = FormSeverity,
                    MessageZhTw = FormMessageZhTw,
                    MessageEnUs = FormMessageEnUs,
                    MessageViVn = FormMessageViVn
                };

                if (EditingId == null)
                    await _dataService.AddErrorListAsync(dto);
                else
                    await _dataService.UpdateErrorListAsync(dto);

                FormSuccessString = EditingId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _log.AddLog($"儲存錯誤代碼失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void ClearForm()
        {
            EditingId = null;
            IsNewMode = true;
            FormErrorCode = "";
            FormCategory = null;
            FormSeverity = 0;
            FormMessageZhTw = null;
            FormMessageEnUs = null;
            FormMessageViVn = null;
        }
    }
}
