using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using FProductionDashBoard.Models;

namespace FProductionDashBoard.Repositories
{
    public class EmployeeRepository : Repository<Employee, MesDbContext>, IEmployeeRepository
    {
        public EmployeeRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }



    }
}
