using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class RolePermissionRepository : Repository<Role, MesDbContext>, IRolePermissionRepository
    {
        public RolePermissionRepository(MesDbContext context) : base(context)
        {
        }

        /// <summary>
        /// 取得所有權限清單
        /// </summary>
        public async Task<List<Permission>> GetAllPermissionsAsync()
        {
            return await _context.Permissions.ToListAsync();
        }

        /// <summary>
        /// 取得所有角色清單 (含權限)
        /// </summary>
        public async Task<List<Role>> GetAllRolesAsync()
        {
            return await _context.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .ToListAsync();
        }
        /// <summary>
        /// 取得指定員工的所有權限
        /// </summary>
        public async Task<List<Permission>> GetEmployeePermissionsAsync(int employeeId)
        {
            return await _context.Employees
                .Where(e => e.EmployeeId == employeeId)
                .Include(e => e.Role)
                    .ThenInclude(r => (r ?? new()).RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .SelectMany(e => e.Role!.RolePermissions.Select(rp => rp.Permission!))
                .ToListAsync();
        }
    }
}
