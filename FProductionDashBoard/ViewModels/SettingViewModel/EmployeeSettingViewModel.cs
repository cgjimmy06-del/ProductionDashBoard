using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FProductionDashBoard.ViewModels
{
    public partial class EmployeeSettingViewModel : SettingViewModelBase
    {
        public ObservableCollection<Employee> EmployeeList { get; } = new();
        public ObservableCollection<Role> RoleItems { get; } = new();

        [ObservableProperty] private int? editingId;
        [ObservableProperty] private string formUserId = "";
        [ObservableProperty] private string formName = "";
        [ObservableProperty] private string formPassword = "";
        [ObservableProperty] private int? formRoleId;
        [ObservableProperty] private string? formCardId;
        [ObservableProperty] private string? formEmail;
        [ObservableProperty] private string? formDepartmentId;

        public bool IsEditMode => EditingId != null;

        public EmployeeSettingViewModel(DashboardCoreServices core)
            : base(core) { }

        partial void OnEditingIdChanged(int? value) => OnPropertyChanged(nameof(IsEditMode));

        protected override async Task LoadAsync()
        {
            try
            {
                if (!RoleItems.Any())
                {
                    var roles = await _core.Data.GetAllRolesAsync();
                    foreach (var r in roles) RoleItems.Add(r);
                }

                var employees = await _core.Data.GetAllEmployeesAsync();
                var employeesNoSystem = employees.Where(e => e.UserId != "admin" && e.UserId != "visitor");

                EmployeeList.Clear();
                foreach (var e in employeesNoSystem) EmployeeList.Add(e);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"載入員工清單失敗: {ex.Message}", LogLevel.Error);
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
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseDelete}?")) 
                return;
            try
            {
                await _core.Data.DeleteEmployeeAsync(item.EmployeeId);
                EmployeeList.Remove(item);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"刪除員工失敗 (檢查是否有關聯紀錄): {ex.Message}", LogLevel.Error);
            }
        }

        protected override async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormUserId) || string.IsNullOrWhiteSpace(FormName))
            { FormErrorString = "UserId、姓名為必填"; return; }
            if (FormRoleId == null)
            { FormErrorString = "請確實設定員工角色"; return; }
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
                    RoleId = FormRoleId ?? 1,
                    CardId = string.IsNullOrWhiteSpace(FormCardId) ? null : FormCardId,
                    Email = string.IsNullOrWhiteSpace(FormEmail) ? null : FormEmail,
                    DepartmentId = string.IsNullOrWhiteSpace(FormDepartmentId) ? null : FormDepartmentId
                };

                if (EditingId == null)
                    await _core.Data.AddEmployeeAsync(dto);
                else
                    await _core.Data.UpdateEmployeeAsync(dto);

                FormSuccessString = EditingId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _core.Log.AddLog($"儲存員工失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void ClearForm()
        {
            EditingId = null;
            FormUserId = "";
            FormName = "";
            FormPassword = "";
            FormRoleId = 1;
            FormCardId = null;
            FormEmail = null;
            FormDepartmentId = null;
        }
    }
}
