using CommunityToolkit.Mvvm.Input;
using FProductionDashBoard.Dtos;
using FProductionDashBoard.Models;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using FProductionDashBoard.ViewModels;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.ViewModels
{
    public class RolePermissionSettingViewModelTests
    {
        // 驗證 Part 3-B：僅具 special 權限者才看得到 / 可勾 special(10)、test(11) 標籤；
        // 且非 special 者編輯「已含 special 的角色」時，存檔不會誤刪 special（view 層隱藏、item 保留）。

        private readonly Mock<IDataService> _data = new();
        private readonly Mock<IDialogService> _dialog = new();

        private RolePermissionSettingViewModel CreateVm(params int[] permissions)
        {
            var log = new LogService();
            var auth = new AuthorizationService();
            auth.SetCachedRoles(new List<Role>
            {
                new()
                {
                    RoleId = 5,
                    RolePermissions = permissions
                        .Select(p => new RolePermission { RoleId = 5, PermissionId = p })
                        .ToList(),
                },
            });
            auth.InitializeAsync(new UserInfo { UserId = "u1", Name = "n1", RoleId = 5 })
                .GetAwaiter().GetResult();

            var cardReader = new Mock<ICardReaderService>().Object;
            var core = new DashboardCoreServices(log, _data.Object, auth, cardReader, new Mock<IWarehouseService>().Object);
            return new RolePermissionSettingViewModel(core, _dialog.Object);
        }

        private void SetupCatalog()
        {
            _data.Setup(d => d.GetAllRolesAsync()).ReturnsAsync(new List<Role>());
            _data.Setup(d => d.GetAllPermissionsAsync()).ReturnsAsync(new List<Permission>
            {
                new() { PermissionId = PermissionId.View, Name = "view" },
                new() { PermissionId = PermissionId.Edit, Name = "edit" },
                new() { PermissionId = PermissionId.Special, Name = "special" },
                new() { PermissionId = PermissionId.Test, Name = "test" },
            });
        }

        private static async Task Load(RolePermissionSettingViewModel vm)
            => await ((IAsyncRelayCommand)vm.LoadCommand).ExecuteAsync(null);

        private static PermissionCheckItem Item(RolePermissionSettingViewModel vm, int permissionId)
            => vm.PermissionItems.First(i => i.Permission.PermissionId == permissionId);

        [Fact]
        public async Task LoadAsync_NonSpecialUser_HidesSpecialAndTest()
        {
            SetupCatalog();
            var vm = CreateVm(PermissionId.Setting); // 有 setting、無 special
            await Load(vm);

            Assert.False(Item(vm, PermissionId.Special).IsVisible);
            Assert.False(Item(vm, PermissionId.Test).IsVisible);
            Assert.True(Item(vm, PermissionId.View).IsVisible);
            Assert.True(Item(vm, PermissionId.Edit).IsVisible);
        }

        [Fact]
        public async Task LoadAsync_SpecialUser_AllPermissionsVisible()
        {
            SetupCatalog();
            var vm = CreateVm(PermissionId.Special);
            await Load(vm);

            Assert.All(vm.PermissionItems, i => Assert.True(i.IsVisible));
        }

        [Fact]
        public async Task Save_NonSpecialUser_EditingRoleWithSpecial_KeepsSpecial()
        {
            SetupCatalog();
            RoleFormDto? captured = null;
            _data.Setup(d => d.UpdateRoleAsync(It.IsAny<RoleFormDto>()))
                .Callback<RoleFormDto>(d => captured = d)
                .Returns(Task.CompletedTask);
            _dialog.Setup(d => d.ShowConfirm(It.IsAny<string>())).Returns(true);

            var vm = CreateVm(PermissionId.Setting);
            await Load(vm);

            // 既有角色本來就含 special（隱藏但仍應保留）
            var role = new Role
            {
                RoleId = 3,
                Name = "Supervisor",
                RolePermissions = new List<RolePermission>
                {
                    new() { RoleId = 3, PermissionId = PermissionId.Edit },
                    new() { RoleId = 3, PermissionId = PermissionId.Special },
                },
            };
            vm.EditCommand.Execute(role);
            await ((IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

            Assert.NotNull(captured);
            Assert.Contains(PermissionId.Special, captured!.SelectedPermissionIds);
            Assert.Contains(PermissionId.Edit, captured.SelectedPermissionIds);
        }
    }
}
