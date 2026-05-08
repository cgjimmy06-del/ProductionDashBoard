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
        private readonly Repositories.IRolePermissionRepository _rolePermissionRepo;
        private HashSet<int> _userPermissions = [];

        private readonly UiModels.UserInfo _defaultUser = 
            new UiModels.UserInfo { UserId = "visitor", Name = "訪客", RoleId = 1, Id = 2 };

        public UiModels.UserInfo? CurrentUser { get; private set; }
        public bool IsLoggedIn => CurrentUser is not null && CurrentUser.UserId != _defaultUser.UserId;

        public event Action? UserChanged;

        public AuthorizationService(Repositories.IRolePermissionRepository rolePermissionRepo)
        {
            _rolePermissionRepo = rolePermissionRepo;
        }

        public async Task InitializeAsync(UiModels.UserInfo user)
        {
            CurrentUser = user;

            // 訪客不需連線，直接套用預設唯讀權限
            if (user.UserId == "visitor") _userPermissions = [PermissionId.View];
            else
            {
                try
                {
                    var roles = await _rolePermissionRepo.GetAllRolesAsync().ConfigureAwait(false);
                    var role = roles.FirstOrDefault(r => r.RoleId == user.RoleId);
                    _userPermissions = role?.RolePermissions
                        .Select(rp => rp.PermissionId)
                        .ToHashSet() ?? [];
                }
                catch
                {
                    // 連線失敗時降為訪客離線模式
                    CurrentUser = _defaultUser;
                    _userPermissions = [PermissionId.View];
                }
            }
            UserChanged?.Invoke(); // 呼叫端
        }

        public async Task LogoutAsync()
        {
            await InitializeAsync(_defaultUser).ConfigureAwait(false);
        }

        public bool HasPermission(int permissionId) => _userPermissions.Contains(permissionId);
    }
}
