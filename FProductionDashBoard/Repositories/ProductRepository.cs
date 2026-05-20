using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public class ProductRepository : Repository<Product, MesDbContext>, IProductRepository
    {
        public ProductRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }

        public async Task<List<Product>> GetAllWithDetailsAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Products
                .Include(p => p.Part)
                .Include(p => p.Model)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<List<ProductModel>> GetModelsAsync()
        {
            await using var ctx = _factory.CreateDbContext();
            return await ctx.Set<ProductModel>().ToListAsync().ConfigureAwait(false);
        }
    }
}
