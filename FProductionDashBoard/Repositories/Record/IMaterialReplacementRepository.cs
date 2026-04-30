using FProductionDashBoard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IMaterialReplacementRepository : IRepository<MaterialReplacementRecord, MesDbContext>
    {
        public Task<int> AddReplacementRecordAsync(int equipmentId, int employeeId, string errorCode,
                                                                List<(int materialId, int quantity)> details,
                                                                DateTime? operatedAt = null);
    }
}
