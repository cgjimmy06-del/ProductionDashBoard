using FProductionDashBoard.Models;
using Microsoft.EntityFrameworkCore;

namespace FProductionDashBoard.Repositories
{
    public class ProductPartRepository : Repository<ProductPart, MesDbContext>, IProductPartRepository
    {
        public ProductPartRepository(IDbContextFactory<MesDbContext> factory) : base(factory)
        {
        }
    }
}
