using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class MaterialRepository : Repository<Material, MesDbContext>, IMaterialRepository
    {
        public MaterialRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<List<MaterialType>> GetMaterialTypesAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Set<MaterialType>().ToListAsync().ConfigureAwait(false);
        }
    }
}
