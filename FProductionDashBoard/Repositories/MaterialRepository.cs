using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class MaterialRepository : Repository<Material, MesDbContext>, IMaterialRepository
    {
        public MaterialRepository(MesDbContext context) : base(context)
        {
        }



    }
}
