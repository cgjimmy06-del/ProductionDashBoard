using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class EquipmentProductRepository : Repository<EquipmentProduct, MesDbContext>, IEquipmentProductRepository
    {
        public EquipmentProductRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<List<EquipmentProduct>> GetByEquipmentAsync(int equipmentId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.EquipmentProducts
                .Where(ep => ep.EquipmentId == equipmentId)
                .OrderBy(ep => ep.SeqNo)
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }
}
