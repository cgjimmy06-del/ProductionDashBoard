using FProductionDashBoard.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IProductRepository : IRepository<Product, MesDbContext>
    {
        Task<List<Product>> GetAllWithDetailsAsync();
        Task<List<ProductModel>> GetModelsAsync();
    }
}
