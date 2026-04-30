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
        public RolePermissionRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        /// <summary>
        /// 取得所有權限清單
        /// </summary>
        public async Task<List<Permission>> GetAllPermissionsAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Permissions.ToListAsync();
        }

        /// <summary>
        /// 取得所有角色清單 (含權限)
        /// </summary>
        public async Task<List<Role>> GetAllRolesAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .ToListAsync();
        }

        /// <summary>
        /// 取得指定員工的所有權限
        /// </summary>
        public async Task<List<Permission>> GetEmployeePermissionsAsync(int employeeId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Employees
                .Where(e => e.EmployeeId == employeeId)
                .Include(e => e.Role)
                    .ThenInclude(r => (r ?? new()).RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .SelectMany(e => e.Role!.RolePermissions.Select(rp => rp.Permission!))
                .ToListAsync();
        }

        public async Task AddRoleWithPermissionsAsync(int roleId, string name, string? description, List<int> permissionIds)
        {
            await using var ctx = _factory.CreateDbContext();
            ctx.Roles.Add(new Role { RoleId = roleId, Name = name, Description = description });
            foreach (var pid in permissionIds)
                ctx.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = pid });
            await ctx.SaveChangesAsync();
        }

        public async Task UpdateRoleWithPermissionsAsync(int roleId, string name, string? description, List<int> permissionIds)
        {
            await using var ctx = _factory.CreateDbContext();
            var role = await ctx.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.RoleId == roleId)
                ?? throw new InvalidOperationException($"Role id={roleId} not found");
            role.Name = name;
            role.Description = description;
            ctx.RolePermissions.RemoveRange(role.RolePermissions);
            foreach (var pid in permissionIds)
                ctx.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = pid });
            await ctx.SaveChangesAsync();
        }

        public async Task<bool> HasEmployeesByRoleAsync(int roleId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Employees.AnyAsync(e => e.RoleId == roleId);
        }
    }
}
