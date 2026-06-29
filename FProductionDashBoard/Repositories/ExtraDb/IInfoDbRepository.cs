using FProductionDashBoard.Models.Extra;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories.ExtraDb
{
    public interface IInfoDbRepository
    {
        Task<bool> CheckConnectionAsync();
        Task<string?> GetCustomerByMediumAsync(string mediumCode);
        Task<IEnumerable<MesDevice>> GetAllMesDevicesAsync();
        Task AddMesDeviceAsync(MesDevice entity);
        Task UpdateMesDeviceAsync(MesDevice entity);
    }
}
