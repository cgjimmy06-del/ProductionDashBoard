using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class EmployeeSettingViewModel : SettingViewModelBase
    {
        public ObservableCollection<Employee> EmployeeList { get; } = new();

        public List<RoleItem> RoleItems { get; } = Enum.GetValues<RoleId>()
            .Where(r => r != RoleId.None)
            .Select(r => new RoleItem(r))
            .ToList();

        [ObservableProperty] private int? editingId;
        [ObservableProperty] private string formUserId = "";
        [ObservableProperty] private string formName = "";
        [ObservableProperty] private string formPassword = "";
        [ObservableProperty] private int formRoleId = (int)RoleId.Operator;
        [ObservableProperty] private string? formCardId;
        [ObservableProperty] private string? formEmail;
        [ObservableProperty] private string? formDepartmentId;

        public bool IsEditMode => EditingId != null;

        public EmployeeSettingViewModel(LogService log, IDataService dataService)
            : base(log, dataService) { }

        partial void OnEditingIdChanged(int? value) => OnPropertyChanged(nameof(IsEditMode));

        protected override async Task LoadAsync()
        {
            try
            {
                var list = await _dataService.GetAllEmployeesAsync();
                EmployeeList.Clear();
                foreach (var e in list) EmployeeList.Add(e);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _log.AddLog($"載入員工清單失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OpenNewForm()
        {
            EditingId = null;
            ClearForm();
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void Edit(Employee item)
        {
            EditingId = item.EmployeeId;
            FormUserId = item.UserId;
            FormName = item.Name;
            FormPassword = "";
            FormRoleId = item.RoleId;
            FormCardId = item.CardId;
            FormEmail = item.Email;
            FormDepartmentId = item.DepartmentId;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private async Task Delete(Employee item)
        {
            try
            {
                await _dataService.DeleteEmployeeAsync(item.EmployeeId);
                EmployeeList.Remove(item);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _log.AddLog($"刪除員工失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormUserId) || string.IsNullOrWhiteSpace(FormName))
            { FormErrorString = "UserId、姓名為必填"; return; }
            if (EditingId == null && string.IsNullOrWhiteSpace(FormPassword))
            { FormErrorString = "新增員工時密碼為必填"; return; }

            try
            {
                var dto = new EmployeeFormDto
                {
                    Id = EditingId,
                    UserId = FormUserId,
                    Name = FormName,
                    Password = FormPassword,
                    RoleId = FormRoleId,
                    CardId = string.IsNullOrWhiteSpace(FormCardId) ? null : FormCardId,
                    Email = string.IsNullOrWhiteSpace(FormEmail) ? null : FormEmail,
                    DepartmentId = string.IsNullOrWhiteSpace(FormDepartmentId) ? null : FormDepartmentId
                };

                if (EditingId == null)
                    await _dataService.AddEmployeeAsync(dto);
                else
                    await _dataService.UpdateEmployeeAsync(dto);

                FormSuccessString = EditingId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _log.AddLog($"儲存員工失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void ClearForm()
        {
            EditingId = null;
            FormUserId = "";
            FormName = "";
            FormPassword = "";
            FormRoleId = (int)RoleId.Operator;
            FormCardId = null;
            FormEmail = null;
            FormDepartmentId = null;
        }
    }

    public class RoleItem
    {
        public int Value { get; }
        public string DisplayName { get; }

        public RoleItem(RoleId role)
        {
            Value = (int)role;
            var field = typeof(RoleId).GetField(role.ToString());
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            DisplayName = attr?.Description ?? role.ToString();
        }
    }
}
