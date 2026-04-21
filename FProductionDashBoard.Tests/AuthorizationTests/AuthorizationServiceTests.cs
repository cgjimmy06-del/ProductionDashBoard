using FProductionDashBoard.Services;
using FProductionDashBoard.UiModels;
using Xunit;

namespace FProductionDashBoard.Tests.AuthorizationTests
{
    public class AuthorizationServiceTests
    {
        private static UserInfo MakeUser(int roleId) =>
            new() { UserId = "u1", Name = "user", RoleId = roleId };

        // ─── Constructor ──────────────────────────────────────────────────────────

        [Fact]
        public void Constructor_InvalidRoleId_DefaultsToViewer()
        {
            var svc = new AuthorizationService(MakeUser(999));
            Assert.True(svc.HasPermission(Permission.View));
            Assert.False(svc.HasPermission(Permission.Edit));
        }

        [Fact]
        public void Constructor_NoneRole_GrantsNoPermissions()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.None));
            foreach (Permission p in Enum.GetValues<Permission>())
                Assert.False(svc.HasPermission(p), $"None role should not have {p}");
        }

        // ─── Admin ────────────────────────────────────────────────────────────────

        [Fact]
        public void HasPermission_AdminRole_HasAllPermissions()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Admin));
            foreach (Permission p in Enum.GetValues<Permission>())
                Assert.True(svc.HasPermission(p), $"Admin should have {p}");
        }

        // ─── Per-role permission matrix ───────────────────────────────────────────

        [Theory]
        [InlineData((int)RoleId.Viewer,     Permission.View,              true)]
        [InlineData((int)RoleId.Viewer,     Permission.Edit,              false)]
        [InlineData((int)RoleId.Viewer,     Permission.OperateMaterial,   false)]
        [InlineData((int)RoleId.Inspector,  Permission.View,              true)]
        [InlineData((int)RoleId.Inspector,  Permission.OperateInspection, true)]
        [InlineData((int)RoleId.Inspector,  Permission.OperateMaterial,   false)]
        [InlineData((int)RoleId.Inspector,  Permission.Edit,              false)]
        [InlineData((int)RoleId.Operator,   Permission.View,              true)]
        [InlineData((int)RoleId.Operator,   Permission.OperateMaterial,   true)]
        [InlineData((int)RoleId.Operator,   Permission.OperateTuning,     true)]
        [InlineData((int)RoleId.Operator,   Permission.OperateInspection, false)]
        [InlineData((int)RoleId.Operator,   Permission.Edit,              false)]
        [InlineData((int)RoleId.Scheduler,  Permission.View,              true)]
        [InlineData((int)RoleId.Scheduler,  Permission.Order,             true)]
        [InlineData((int)RoleId.Scheduler,  Permission.Schedule,          true)]
        [InlineData((int)RoleId.Scheduler,  Permission.Edit,              false)]
        [InlineData((int)RoleId.Scheduler,  Permission.OperateMaterial,   false)]
        [InlineData((int)RoleId.Supervisor, Permission.View,              true)]
        [InlineData((int)RoleId.Supervisor, Permission.Setting,           true)]
        [InlineData((int)RoleId.Supervisor, Permission.OperateTuning,     true)]
        [InlineData((int)RoleId.Supervisor, Permission.Delete,            false)]
        [InlineData((int)RoleId.Supervisor, Permission.Special,           false)]
        [InlineData((int)RoleId.Supervisor, Permission.Schedule,          false)]
        [InlineData((int)RoleId.Engineer,   Permission.View,              true)]
        [InlineData((int)RoleId.Engineer,   Permission.Edit,              true)]
        [InlineData((int)RoleId.Engineer,   Permission.OperateTuning,     true)]
        [InlineData((int)RoleId.Engineer,   Permission.Schedule,          true)]
        [InlineData((int)RoleId.Engineer,   Permission.Special,           false)]
        [InlineData((int)RoleId.Engineer,   Permission.Delete,            false)]
        [InlineData((int)RoleId.Engineer,   Permission.Test,              false)]
        public void HasPermission_RolePermissionMatrix(int roleId, Permission permission, bool expected)
        {
            var svc = new AuthorizationService(MakeUser(roleId));
            Assert.Equal(expected, svc.HasPermission(permission));
        }

        // ─── UpdateUser ───────────────────────────────────────────────────────────

        [Fact]
        public void UpdateUser_ChangesPermissionsToNewRole()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Viewer));
            Assert.False(svc.HasPermission(Permission.Edit));

            svc.UpdateUser(MakeUser((int)RoleId.Admin));
            Assert.True(svc.HasPermission(Permission.Edit));
        }

        [Fact]
        public void UpdateUser_InvalidRoleId_DefaultsToViewer()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Admin));
            Assert.True(svc.HasPermission(Permission.Delete));

            svc.UpdateUser(MakeUser(999));
            Assert.True(svc.HasPermission(Permission.View));
            Assert.False(svc.HasPermission(Permission.Delete));
        }

        [Fact]
        public void UpdateUser_ToNoneRole_ClearsAllPermissions()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Admin));

            svc.UpdateUser(MakeUser((int)RoleId.None));

            foreach (Permission p in Enum.GetValues<Permission>())
                Assert.False(svc.HasPermission(p), $"None role should not have {p}");
        }

        // ─── RolesList ────────────────────────────────────────────────────────────

        [Fact]
        public void RolesList_ContainsAllDefinedRoles()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Viewer));
            Assert.Equal(Enum.GetValues<RoleId>().Length, svc.RolesList.Count);
        }

        [Theory]
        [InlineData(RoleId.None,       "未登入")]
        [InlineData(RoleId.Admin,      "管理員")]
        [InlineData(RoleId.Viewer,     "訪客")]
        [InlineData(RoleId.Engineer,   "工程師")]
        [InlineData(RoleId.Supervisor, "現場主管")]
        [InlineData(RoleId.Scheduler,  "生管")]
        [InlineData(RoleId.Operator,   "操作員")]
        [InlineData(RoleId.Inspector,  "品檢員")]
        public void Role_Name_ReturnsChineseDescription(RoleId roleId, string expectedName)
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Viewer));
            var role = svc.RolesList.First(r => r.Id == roleId);
            Assert.Equal(expectedName, role.Name);
        }
    }
}
