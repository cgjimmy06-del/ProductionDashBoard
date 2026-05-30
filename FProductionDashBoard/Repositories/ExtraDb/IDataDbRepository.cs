using FProductionDashBoard.Models.Extra;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories.ExtraDb
{
    public interface IDataDbRepository
    {
        Task<IEnumerable<VwMesDailyProcessData>> GetDailyProcessDataAsync();
    }
}
