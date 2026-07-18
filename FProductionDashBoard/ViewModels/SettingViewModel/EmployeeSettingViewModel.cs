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

        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEditMode))] private int? editingId;
        [ObservableProperty] private string formUserId = "";
        [ObservableProperty] private string formName = "";
        [ObservableProperty] private string formPassword = "";
        [ObservableProperty] private int? formRoleId;
        [ObservableProperty] private string? formCardId;
        [ObservableProperty] private string? formEmail;
        [ObservableProperty] private string? formDepartmentId;

        public bool IsEditMode => EditingId != null;

        public EmployeeSettingViewModel(DashboardCoreServices core, Services.IDialogService dialog)
            : base(core, dialog) { }

        protected override async Task LoadAsync()
        {
            try
            {
                // 每次載入依當前使用者權限重建角色清單 (避免換人登入後 admin 選項殘留/缺漏)
                var canAssignAdmin = _core.Authorization.HasPermission(PermissionId.Special);
                var roles = await _core.Data.GetAllRolesAsync();

                var desiredRoleId = FormRoleId; // 快照 : 避免 Clear() + add() 觸發 SelectedValue 回寫
                RoleItems.Clear();
                foreach (var r in roles)
                {
                    if (!canAssignAdmin && r.RoleId == 0) continue;
                    RoleItems.Add(r);
                }
                FormRoleId = desiredRoleId;

                var employees = await _core.Data.GetAllEmployeesAsync();
                var employeesNoSystem = employees.Where(e => e.UserId != "admin" && e.UserId != "visitor");

                EmployeeList.Clear();
                foreach (var e in employeesNoSystem) EmployeeList.Add(e);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog("[員工] 載入員工清單失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[LoadAsync] {ex.Message}");
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
                _core.Log.AddLog("[員工] 刪除失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[Delete] {ex.Message}");
            }
        }

        protected override async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormUserId) || string.IsNullOrWhiteSpace(FormName))
            { FormErrorString = Properties.Resources.SettingValidationUserIdNameRequired; return; }
            if (FormRoleId == null)
            { FormErrorString = Properties.Resources.SettingValidationRoleRequired; return; }
            if (EditingId == null && string.IsNullOrWhiteSpace(FormPassword))
            { FormErrorString = Properties.Resources.SettingValidationPasswordRequired; return; }

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

                FormSuccessString = EditingId == null ? Properties.Resources.SettingSuccessAdd : Properties.Resources.SettingSuccessUpdate;
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _core.Log.AddLog("[員工] 儲存失敗", LogLevel.Error);
                _core.Log.AddErrorLog($"[SaveAsync] {ex.Message}");
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
