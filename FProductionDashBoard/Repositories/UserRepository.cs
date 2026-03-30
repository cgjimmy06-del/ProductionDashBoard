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
    public class UserRepository : Repository<UserInfo, MesDbContext>, IUserRepository
    {
        private readonly MesDbContext _context;

        public UserRepository(MesDbContext context) : base(context)
        {
            _context = context;
        }



    }
}
