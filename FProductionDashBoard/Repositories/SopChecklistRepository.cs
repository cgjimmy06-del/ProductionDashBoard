using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class SopChecklistRepository : Repository<SopChecklist, MesDbContext>, ISopChecklistRepository
    {
        public SopChecklistRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<SopChecklist?> GetWithItemsAsync(int sopId)
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.SopChecklists
                .Include(s => s.Items)
                    .ThenInclude(i => i.Material)
                .FirstOrDefaultAsync(s => s.SopId == sopId)
                .ConfigureAwait(false);
        }

        public async Task<List<WorkProcess>> GetProcessesAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Set<WorkProcess>().ToListAsync().ConfigureAwait(false);
        }

        public async Task<int> AddProductModelAsync(string name)
        {
            await using var ctx = _factory.CreateDbContext();
            var entity = new ProductModel { Name = name };
            ctx.Set<ProductModel>().Add(entity);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return entity.ModelId;
        }
    }
}
