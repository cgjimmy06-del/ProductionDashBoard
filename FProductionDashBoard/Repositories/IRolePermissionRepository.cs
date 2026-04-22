using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IRolePermissionRepository : IRepository<Role, MesDbContext>
    {
        Task<List<Permission>> GetAllPermissionsAsync();
        Task<List<Role>> GetAllRolesAsync();
        Task<List<Permission>> GetEmployeePermissionsAsync(int employeeId);
    }
}
