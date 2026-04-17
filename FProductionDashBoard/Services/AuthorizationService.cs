using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Printing;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FProductionDashBoard.Services
{
    public enum RoleId
    {
        [Description("未登入")] // [LocalizedDescription("RoleId_Admin", typeof(EnumResources))]
        None = -1,
        [Description("管理員")]
        Admin = 0, // (ALL) (自動操作)
        [Description("訪客")]
        Viewer = 1, // (僅 view) (測試資料)
        [Description("工程師")]
        Engineer = 2, // (異常排除與系統設定)
        [Description("現場主管")]
        Supervisor = 3, // (線長、組長)
        [Description("生管")]
        Scheduler = 4, // 排單人員
        [Description("操作員")]
        Operator = 5, // (調試與生產)
        [Description("品檢員")]
        Inspector = 6, // (首件、巡檢)
    }
    public enum Permission
    {
        /// <summary>
        /// (主頁) 僅查看 Read
        /// </summary>
        View = 1,
        /// <summary>
        /// (主頁) 設定 insert, update
        /// </summary>
        Edit = 2,
        /// <summary>
        /// (主頁) 設定 delete
        /// </summary>
        Delete = 3,
        /// <summary>
        /// (主頁) 環境設定 排版
        /// </summary>
        Setting = 4,
        /// <summary>
        /// (現場看板) 可選擇生產程式
        /// </summary>
        Order = 5,
        /// <summary>
        /// (現場看板) 操作物料
        /// </summary>
        OperateMaterial = 6,
        /// <summary>
        /// (現場看板) 操作品檢
        /// </summary>
        OperateInspection = 7,
        /// <summary>
        /// (現場看板) 操作調試 - 含設備測試 (Test連動)
        /// </summary>
        OperateTuning = 8,
        /// <summary>
        /// (現場看板) 可排單 - 生管
        /// </summary>
        Schedule = 9,
        /// <summary>
        /// (主頁) 特殊操作 (後台 維護 權限)
        /// </summary>
        Special = 10,
        /// <summary>
        /// (主頁) 測試排除 (設備 連線)
        /// </summary>
        Test = 11,
    }
    public class Role
    {
        public RoleId Id { get; set; } = RoleId.Viewer;
        public List<Permission> Permissions { get; set; } = new();
        public string Name => GetEnumDescription(Id);
        private string GetEnumDescription(Enum value)
        {
            FieldInfo? field = value.GetType().GetField(value.ToString());

            // 取得該欄位上的 DescriptionAttribute
            DescriptionAttribute? attribute = field?.GetCustomAttribute<DescriptionAttribute>();

            // 如果有設定 Description 就回傳內容，否則回傳原本的 Enum 名稱 (ToString)
            return attribute?.Description ?? value.ToString();
        }
    }

    public class AuthorizationService
    {
        public List<Role> RolesList { get; set; } = []; // 由此映射，外部僅需知道其角色
        private HashSet<Permission> _userPermissions;
        public AuthorizationService(int roleid) // IEnumerable<Permission> permissions
        {
            GetRolesList();

            if (!Enum.IsDefined(typeof(RoleId), roleid)) roleid = 1; // 如果角色編號沒有被定義，則為訪客

            var permissions = RolesList.First(r => r.Id == (RoleId)roleid).Permissions;
            _userPermissions = [.. permissions];
        }
        public bool HasPermission(Permission permission) =>
            _userPermissions.Contains(permission);
        public void UpdateUser(int roleid)
        {
            if (!Enum.IsDefined(typeof(RoleId), roleid)) roleid = 1; // 如果角色編號沒有被定義，則為訪客

            var permissions = RolesList.First(r => r.Id == (RoleId)roleid).Permissions;
            _userPermissions = [.. permissions];
        }

        private void GetRolesList() // 後續規劃由清單建立，可於UI設定權限
        {
            RolesList.Add(new Role() {
                Id = RoleId.None
            });
            RolesList.Add(new Role() {
                Id = RoleId.Admin,
                Permissions = new List<Permission>() { 
                    Permission.View, Permission.Edit, Permission.Delete, Permission.Setting,
                    Permission.Order, Permission.OperateMaterial, Permission.OperateInspection,
                    Permission.OperateTuning, Permission.Schedule, Permission.Special, Permission.Test
                }});
            RolesList.Add(new Role() {
                Id = RoleId.Viewer,
                Permissions = new List<Permission>() {
                    Permission.View,
                }
            });
            RolesList.Add(new Role() {
                Id = RoleId.Engineer,
                Permissions = new List<Permission>() {
                    Permission.View, Permission.Edit, Permission.Setting,
                    Permission.Order, Permission.OperateMaterial, Permission.OperateInspection,
                    Permission.OperateTuning, Permission.Schedule,
                }
            });
            RolesList.Add(new Role() {
                Id = RoleId.Supervisor,
                Permissions = new List<Permission>() {
                    Permission.View, Permission.Setting,
                    Permission.Order, Permission.OperateMaterial, Permission.OperateInspection,
                    Permission.OperateTuning,
                }
            });
            RolesList.Add(new Role() {
                Id = RoleId.Scheduler,
                Permissions = new List<Permission>() {
                    Permission.View,
                    Permission.Order, Permission.Schedule,
                }
            });
            RolesList.Add(new Role() {
                Id = RoleId.Operator,
                Permissions = new List<Permission>() {
                    Permission.View,
                    Permission.Order, Permission.OperateMaterial,
                    Permission.OperateTuning,
                }
            });
            RolesList.Add(new Role() {
                Id = RoleId.Inspector,
                Permissions = new List<Permission>() {
                    Permission.View,
                    Permission.OperateInspection,
                }
            });
        }
    }

    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private readonly string _resourceKey;
        private readonly ResourceManager _resourceManager;

        public LocalizedDescriptionAttribute(string resourceKey, Type resourceType)
        {
            _resourceKey = resourceKey;
            _resourceManager = new ResourceManager(resourceType);
        }

        public override string Description
        {
            get
            {
                // 根據目前的 UI 文化特性 (CurrentUICulture) 取得翻譯
                string? displayName = _resourceManager.GetString(_resourceKey);
                return string.IsNullOrEmpty(displayName) ? _resourceKey : displayName;
            }
        }
    }

    //public class AuthorizationCommand : ICommand
    //{
    //    private readonly Action _execute;
    //    private readonly Func<bool> _canExecute;
    //    //private readonly AuditLogger _logger;
    //    //private readonly User _user;
    //    //private readonly Permission _permission;

    //    public AuthorizationCommand(Action execute, Func<bool> canExecute
    //                                /*,AuditLogger logger, User user, Permission permission*/)
    //    {
    //        _execute = execute;
    //        _canExecute = canExecute;
    //        //_logger = logger;
    //        //_user = user;
    //        //_permission = permission;
    //    }

    //    public bool CanExecute(object parameter) => _canExecute();

    //    public void Execute(object parameter)
    //    {
    //        //_logger.Log(_user.UserName, _permission, "執行 Command");
    //        _execute();
    //    }

    //    public event EventHandler CanExecuteChanged;
    //    public void RaiseCanExecuteChanged() =>
    //        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    //}
    //public class AuthorizationAsyncCommand : IAsyncRelayCommand
    //{
    //    private readonly Func<Task> _executeAsync;
    //    private readonly Func<bool> _canExecute;
    //    private readonly AuditLogger _logger;
    //    private readonly string _userName;
    //    private readonly Permission _permission;

    //    public AuthorizationAsyncCommand(Func<Task> executeAsync, Func<bool> canExecute,
    //                                     AuditLogger logger, string userName, Permission permission)
    //    {
    //        _executeAsync = executeAsync;
    //        _canExecute = canExecute;
    //        _logger = logger;
    //        _userName = userName;
    //        _permission = permission;
    //    }

    //    public bool CanExecute(object parameter) => _canExecute();

    //    public async Task ExecuteAsync(object parameter)
    //    {
    //        _logger.Log(_userName, _permission, "執行 Async Command");
    //        await _executeAsync();
    //    }

    //    // IAsyncRelayCommand 需要這些成員
    //    public event EventHandler CanExecuteChanged;
    //    public void RaiseCanExecuteChanged() =>
    //        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    //    // 讓 WPF 的 Command 機制能正常運作
    //    void ICommand.Execute(object parameter) => ExecuteAsync(parameter).ConfigureAwait(false);
    //}
}
