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
            Assert.True(svc.HasPermission(AuthPermission.View));
            Assert.False(svc.HasPermission(AuthPermission.Edit));
        }

        [Fact]
        public void Constructor_NoneRole_GrantsNoPermissions()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.None));
            foreach (AuthPermission p in Enum.GetValues<AuthPermission>())
                Assert.False(svc.HasPermission(p), $"None role should not have {p}");
        }

        // ─── Admin ────────────────────────────────────────────────────────────────

        [Fact]
        public void HasPermission_AdminRole_HasAllPermissions()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Admin));
            foreach (AuthPermission p in Enum.GetValues<AuthPermission>())
                Assert.True(svc.HasPermission(p), $"Admin should have {p}");
        }

        // ─── Per-role AuthPermission matrix ───────────────────────────────────────────

        [Theory]
        [InlineData((int)RoleId.Viewer,     AuthPermission.View,              true)]
        [InlineData((int)RoleId.Viewer,     AuthPermission.Edit,              false)]
        [InlineData((int)RoleId.Viewer,     AuthPermission.OperateMaterial,   false)]
        [InlineData((int)RoleId.Inspector,  AuthPermission.View,              true)]
        [InlineData((int)RoleId.Inspector,  AuthPermission.OperateInspection, true)]
        [InlineData((int)RoleId.Inspector,  AuthPermission.OperateMaterial,   false)]
        [InlineData((int)RoleId.Inspector,  AuthPermission.Edit,              false)]
        [InlineData((int)RoleId.Operator,   AuthPermission.View,              true)]
        [InlineData((int)RoleId.Operator,   AuthPermission.OperateMaterial,   true)]
        [InlineData((int)RoleId.Operator,   AuthPermission.OperateTuning,     true)]
        [InlineData((int)RoleId.Operator,   AuthPermission.OperateInspection, false)]
        [InlineData((int)RoleId.Operator,   AuthPermission.Edit,              false)]
        [InlineData((int)RoleId.Scheduler,  AuthPermission.View,              true)]
        [InlineData((int)RoleId.Scheduler,  AuthPermission.Order,             true)]
        [InlineData((int)RoleId.Scheduler,  AuthPermission.Schedule,          true)]
        [InlineData((int)RoleId.Scheduler,  AuthPermission.Edit,              false)]
        [InlineData((int)RoleId.Scheduler,  AuthPermission.OperateMaterial,   false)]
        [InlineData((int)RoleId.Supervisor, AuthPermission.View,              true)]
        [InlineData((int)RoleId.Supervisor, AuthPermission.Setting,           true)]
        [InlineData((int)RoleId.Supervisor, AuthPermission.OperateTuning,     true)]
        [InlineData((int)RoleId.Supervisor, AuthPermission.Delete,            false)]
        [InlineData((int)RoleId.Supervisor, AuthPermission.Special,           false)]
        [InlineData((int)RoleId.Supervisor, AuthPermission.Schedule,          false)]
        [InlineData((int)RoleId.Engineer,   AuthPermission.View,              true)]
        [InlineData((int)RoleId.Engineer,   AuthPermission.Edit,              true)]
        [InlineData((int)RoleId.Engineer,   AuthPermission.OperateTuning,     true)]
        [InlineData((int)RoleId.Engineer,   AuthPermission.Schedule,          true)]
        [InlineData((int)RoleId.Engineer,   AuthPermission.Special,           false)]
        [InlineData((int)RoleId.Engineer,   AuthPermission.Delete,            false)]
        [InlineData((int)RoleId.Engineer,   AuthPermission.Test,              false)]
        public void HasPermission_RolePermissionMatrix(int roleId, AuthPermission AuthPermission, bool expected)
        {
            var svc = new AuthorizationService(MakeUser(roleId));
            Assert.Equal(expected, svc.HasPermission(AuthPermission));
        }

        // ─── UpdateUser ───────────────────────────────────────────────────────────

        [Fact]
        public void UpdateUser_ChangesPermissionsToNewRole()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Viewer));
            Assert.False(svc.HasPermission(AuthPermission.Edit));

            svc.UpdateUser(MakeUser((int)RoleId.Admin));
            Assert.True(svc.HasPermission(AuthPermission.Edit));
        }

        [Fact]
        public void UpdateUser_InvalidRoleId_DefaultsToViewer()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Admin));
            Assert.True(svc.HasPermission(AuthPermission.Delete));

            svc.UpdateUser(MakeUser(999));
            Assert.True(svc.HasPermission(AuthPermission.View));
            Assert.False(svc.HasPermission(AuthPermission.Delete));
        }

        [Fact]
        public void UpdateUser_ToNoneRole_ClearsAllPermissions()
        {
            var svc = new AuthorizationService(MakeUser((int)RoleId.Admin));

            svc.UpdateUser(MakeUser((int)RoleId.None));

            foreach (AuthPermission p in Enum.GetValues<AuthPermission>())
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
