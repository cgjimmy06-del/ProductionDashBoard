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
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        public AuthorizationService(UiModels.UserInfo nuser) // 之後直接由此注入 RolesList 或是UsersList 已存在每位員工的permissions
        {
            GetRolesList();

            if (!Enum.IsDefined(typeof(RoleId), nuser.RoleId)) nuser.RoleId = 1; // 如果角色編號定義異常，則為訪客

            var permissions = RolesList.First(r => r.Id == (RoleId)nuser.RoleId).Permissions;
            _userPermissions = [.. permissions];
        }
        public bool HasPermission(Permission permission) =>
            _userPermissions.Contains(permission);
        public void UpdateUser(UiModels.UserInfo nuser)
        {
            if (!Enum.IsDefined(typeof(RoleId), nuser.RoleId)) nuser.RoleId = 1; // 如果角色編號定義異常，則為訪客

            var permissions = RolesList.First(r => r.Id == (RoleId)nuser.RoleId).Permissions;
            _userPermissions = [.. permissions];
        }

        private void GetRolesList() // 後續規劃由清單建立，可於UI設定權限 (DI取代此函式)
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
}
