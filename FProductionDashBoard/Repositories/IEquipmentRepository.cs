using FProductionDashBoard.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FProductionDashBoard.Repositories
{
    public interface IEquipmentRepository : IRepository<Equipment, MesDbContext>
    {
        public Task<List<EquipmentType>> GetEquipmentTypesAsync();
        public Task<List<Equipment>> GetAllWithTypeAsync();
    }


}
