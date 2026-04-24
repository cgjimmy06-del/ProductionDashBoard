using FProductionDashBoard.Models;
using FProductionDashBoard.Repositories;
using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.AuthorizationTests
{
    public class AuthorizationServiceTests
    {
        private static async Task<AuthorizationService> CreateService(int roleId, params int[] permissionIds)
        {
            var repo = new Mock<IRolePermissionRepository>();
            var role = new Role
            {
                RoleId = roleId,
                RolePermissions = permissionIds
                    .Select(id => new RolePermission { RoleId = roleId, PermissionId = id })
                    .ToList()
            };
            repo.Setup(r => r.GetAllRolesAsync()).ReturnsAsync(new List<Role> { role });

            var svc = new AuthorizationService(repo.Object);
            await svc.InitializeAsync(new UserInfo { UserId = "u1", Name = "n1", RoleId = roleId });
            return svc;
        }

        private static async Task<AuthorizationService> CreateNoRoleService()
        {
            var repo = new Mock<IRolePermissionRepository>();
            repo.Setup(r => r.GetAllRolesAsync()).ReturnsAsync(new List<Role>());
            var svc = new AuthorizationService(repo.Object);
            await svc.InitializeAsync(new UserInfo { UserId = "u1", Name = "n1", RoleId = 99 });
            return svc;
        }

        // ─── InitializeAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task InitializeAsync_SetsCurrentUser()
        {
            var svc = await CreateService(1, PermissionId.View);
            Assert.Equal("u1", svc.CurrentUser?.UserId);
        }

        [Fact]
        public async Task InitializeAsync_LoadsPermissionsFromRole()
        {
            var svc = await CreateService(1, PermissionId.View, PermissionId.Edit);
            Assert.True(svc.HasPermission(PermissionId.View));
            Assert.True(svc.HasPermission(PermissionId.Edit));
            Assert.False(svc.HasPermission(PermissionId.Delete));
        }

        [Fact]
        public async Task InitializeAsync_UnknownRoleId_GrantsNoPermissions()
        {
            var svc = await CreateNoRoleService();
            Assert.False(svc.HasPermission(PermissionId.View));
        }

        [Fact]
        public async Task InitializeAsync_RaisesUserChangedEvent()
        {
            var repo = new Mock<IRolePermissionRepository>();
            repo.Setup(r => r.GetAllRolesAsync()).ReturnsAsync(new List<Role>());
            var svc = new AuthorizationService(repo.Object);

            bool eventRaised = false;
            svc.UserChanged += () => eventRaised = true;

            await svc.InitializeAsync(new UserInfo { UserId = "u1", Name = "n1", RoleId = 1 });
            Assert.True(eventRaised);
        }

        // ─── HasPermission ────────────────────────────────────────────────────────

        [Fact]
        public async Task HasPermission_WithMatchingPermissionId_ReturnsTrue()
        {
            var svc = await CreateService(1, PermissionId.View, PermissionId.Setting);
            Assert.True(svc.HasPermission(PermissionId.View));
            Assert.True(svc.HasPermission(PermissionId.Setting));
        }

        [Fact]
        public async Task HasPermission_WithNonMatchingPermissionId_ReturnsFalse()
        {
            var svc = await CreateService(1, PermissionId.View);
            Assert.False(svc.HasPermission(PermissionId.Edit));
            Assert.False(svc.HasPermission(PermissionId.Test));
        }

        // ─── IsLoggedIn ──────────────────────────────────────────────────────────

        [Fact]
        public async Task IsLoggedIn_RealUser_ReturnsTrue()
        {
            var svc = await CreateService(1, PermissionId.View);
            Assert.True(svc.IsLoggedIn);
        }

        [Fact]
        public async Task IsLoggedIn_VisitorUser_ReturnsFalse()
        {
            var repo = new Mock<IRolePermissionRepository>();
            repo.Setup(r => r.GetAllRolesAsync()).ReturnsAsync(new List<Role>());
            var svc = new AuthorizationService(repo.Object);
            await svc.InitializeAsync(new UserInfo { UserId = "visitor", Name = "n1", RoleId = 1 });
            Assert.False(svc.IsLoggedIn);
        }

        // ─── InitializeAsync offline fallback ────────────────────────────────────

        [Fact]
        public async Task InitializeAsync_DbThrows_FallsBackToVisitor()
        {
            var repo = new Mock<IRolePermissionRepository>();
            repo.Setup(r => r.GetAllRolesAsync()).ThrowsAsync(new Exception("connection failed"));
            var svc = new AuthorizationService(repo.Object);

            await svc.InitializeAsync(new UserInfo { UserId = "admin", Name = "Admin", RoleId = 0 });

            Assert.Equal("visitor", svc.CurrentUser?.UserId);
            Assert.False(svc.IsLoggedIn);
            Assert.True(svc.HasPermission(PermissionId.View));
            Assert.False(svc.HasPermission(PermissionId.Edit));
        }

        // ─── LogoutAsync ─────────────────────────────────────────────────────────

        [Fact]
        public async Task LogoutAsync_SetsCurrentUserToVisitor()
        {
            var repo = new Mock<IRolePermissionRepository>();
            repo.Setup(r => r.GetAllRolesAsync()).ReturnsAsync(new List<Role>());
            var svc = new AuthorizationService(repo.Object);
            await svc.InitializeAsync(new UserInfo { UserId = "admin", Name = "n1", RoleId = 0 });

            await svc.LogoutAsync();

            Assert.Equal("visitor", svc.CurrentUser?.UserId);
            Assert.False(svc.IsLoggedIn);
        }

        [Fact]
        public async Task LogoutAsync_RaisesUserChangedEvent()
        {
            var repo = new Mock<IRolePermissionRepository>();
            repo.Setup(r => r.GetAllRolesAsync()).ReturnsAsync(new List<Role>());
            var svc = new AuthorizationService(repo.Object);
            await svc.InitializeAsync(new UserInfo { UserId = "admin", Name = "n1", RoleId = 0 });

            int eventCount = 0;
            svc.UserChanged += () => eventCount++;

            await svc.LogoutAsync();
            Assert.Equal(1, eventCount);
        }
    }
}
