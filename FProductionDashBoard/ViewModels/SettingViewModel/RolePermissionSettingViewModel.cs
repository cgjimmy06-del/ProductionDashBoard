using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FProductionDashBoard.Models;

namespace FProductionDashBoard.ViewModels
{
    public partial class RolePermissionSettingViewModel : SettingViewModelBase
    {
        public ObservableCollection<Role> RoleList { get; } = new();
        public ObservableCollection<PermissionCheckItem> PermissionItems { get; } = new();

        [ObservableProperty] private int? editingId;
        [ObservableProperty] private int formRoleId;
        [ObservableProperty] private string formName = "";
        [ObservableProperty] private string? formDescription;

        public RolePermissionSettingViewModel(DashboardCoreServices core, Services.IDialogService dialog)
            : base(core, dialog) { }

        protected override async Task LoadAsync()
        {
            try
            {
                var roles = await _core.Data.GetAllRolesAsync();
                var perms = await _core.Data.GetAllPermissionsAsync();

                RoleList.Clear();
                foreach (var r in roles) RoleList.Add(r);

                PermissionItems.Clear();
                foreach (var p in perms) PermissionItems.Add(new PermissionCheckItem(p, false));
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"載入角色權限清單失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OpenNewForm()
        {
            EditingId = null;
            FormRoleId = RoleList.Count > 0 ? RoleList.Max(r => r.RoleId) + 1 : 1;
            FormName = "";
            FormDescription = null;
            foreach (var item in PermissionItems) item.IsSelected = false;
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void Edit(Models.Role item)
        {
            EditingId = item.RoleId;
            FormRoleId = item.RoleId;
            FormName = item.Name ?? "";
            FormDescription = item.Description;
            var selectedIds = item.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
            foreach (var pi in PermissionItems)
                pi.IsSelected = selectedIds.Contains(pi.Permission.PermissionId);
            FormErrorString = null;
            FormSuccessString = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private async Task Delete(Models.Role item)
        {
            if (!ShowConfirm($"{Properties.Resources.DialogBaseConfirm} {Properties.Resources.DialogBaseDelete}?"))
                return;
            try
            {
                await _core.Data.DeleteRoleAsync(item.RoleId);
                RoleList.Remove(item);
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                _core.Log.AddLog($"刪除角色失敗 (檢查是否有關聯紀錄): {ex.Message}", LogLevel.Error);
            }
        }

        protected override async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormName))
            { FormErrorString = "角色名稱為必填"; return; }

            try
            {
                var dto = new RoleFormDto
                {
                    Id = EditingId,
                    RoleId = FormRoleId,
                    Name = FormName,
                    Description = FormDescription,
                    SelectedPermissionIds = PermissionItems
                        .Where(p => p.IsSelected)
                        .Select(p => p.Permission.PermissionId)
                        .ToList()
                };

                if (EditingId == null)
                    await _core.Data.AddRoleAsync(dto);
                else
                    await _core.Data.UpdateRoleAsync(dto);

                FormSuccessString = EditingId == null ? "新增成功" : "更新成功";
                FormErrorString = null;
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                FormErrorString = ex.Message;
                FormSuccessString = null;
                _core.Log.AddLog($"儲存角色失敗: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void ClearForm()
        {
            EditingId = null;
            FormRoleId = 0;
            FormName = "";
            FormDescription = null;
            foreach (var item in PermissionItems) item.IsSelected = false;
        }
    }
}
