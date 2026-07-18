using CommunityToolkit.Mvvm.Input;
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
    public class EmployeeSettingViewModelTests
    {
        // 驗證 Part 3-A：僅具 special 權限者的角色下拉才含 admin（role 0）。

        private readonly Mock<IDataService> _data = new();
        private readonly Mock<IDialogService> _dialog = new();

        private EmployeeSettingViewModel CreateVm(params int[] permissions)
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
            var core = new DashboardCoreServices(log, _data.Object, auth, cardReader);
            return new EmployeeSettingViewModel(core, _dialog.Object);
        }

        private void SetupRolesAndEmployees()
        {
            _data.Setup(d => d.GetAllRolesAsync()).ReturnsAsync(new List<Role>
            {
                new() { RoleId = 0, Name = "Admin" },
                new() { RoleId = 1, Name = "Viewer" },
                new() { RoleId = 5, Name = "Operator" },
            });
            _data.Setup(d => d.GetAllEmployeesAsync()).ReturnsAsync(new List<Employee>());
        }

        private static async Task Load(EmployeeSettingViewModel vm)
            => await ((IAsyncRelayCommand)vm.LoadCommand).ExecuteAsync(null);

        [Fact]
        public async Task LoadAsync_NonSpecialUser_RoleItemsExcludesAdmin()
        {
            SetupRolesAndEmployees();
            var vm = CreateVm(PermissionId.Setting); // 有 setting、無 special
            await Load(vm);

            Assert.DoesNotContain(vm.RoleItems, r => r.RoleId == 0);
            Assert.Contains(vm.RoleItems, r => r.RoleId == 1);
            Assert.Contains(vm.RoleItems, r => r.RoleId == 5);
        }

        [Fact]
        public async Task LoadAsync_SpecialUser_RoleItemsIncludesAdmin()
        {
            SetupRolesAndEmployees();
            var vm = CreateVm(PermissionId.Special);
            await Load(vm);

            Assert.Contains(vm.RoleItems, r => r.RoleId == 0);
        }
    }
}
