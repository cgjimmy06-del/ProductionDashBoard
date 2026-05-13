using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
    // 與 DB permission_id 對應，int 值不可任意更改
    public static class PermissionId
    {
        public const int View = 1;
        public const int Edit = 2;
        public const int Delete = 3;
        public const int Setting = 4;
        public const int Order = 5;
        public const int OperateMaterial = 6;
        public const int OperateInspection = 7;
        public const int OperateTuning = 8;
        public const int Schedule = 9;
        public const int Special = 10;
        public const int Test = 11;
    }

    public class AuthorizationService
    {
        private List<Models.Role>? _cachedRoles;
        private HashSet<int> _userPermissions = [];

        private readonly UiModels.UserInfo _defaultUser =
            new UiModels.UserInfo { UserId = "visitor", Name = "訪客", RoleId = 1, Id = 2 };

        public UiModels.UserInfo? CurrentUser { get; private set; }
        public bool IsLoggedIn => CurrentUser is not null && CurrentUser.UserId != _defaultUser.UserId;

        public event Action? UserChanged;

        public void SetCachedRoles(List<Models.Role> roles)
        {
            _cachedRoles = roles;
        }

        public Task InitializeAsync(UiModels.UserInfo user)
        {
            CurrentUser = user;

            if (user.UserId == _defaultUser.UserId)
                _userPermissions = [PermissionId.View];
            else if (_cachedRoles != null)
            {
                var role = _cachedRoles.FirstOrDefault(r => r.RoleId == user.RoleId);
                _userPermissions = role?.RolePermissions
                    .Select(rp => rp.PermissionId)
                    .ToHashSet() ?? [];
            }
            else
            {
                // 從未取得角色資料（完全離線啟動），降為訪客
                CurrentUser = _defaultUser;
                _userPermissions = [PermissionId.View];
            }

            UserChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task LogoutAsync() => InitializeAsync(_defaultUser);

        public bool HasPermission(int permissionId) => _userPermissions.Contains(permissionId);

        public bool HasAnyPermission(params int[] permissionIds)
            => permissionIds.Any(id => _userPermissions.Contains(id));
    }
}
