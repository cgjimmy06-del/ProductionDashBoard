using FProductionDashBoard.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
    public interface IDataService
    {
        public IEquipmentRepository EquipmentRep { get; }
        public IEmployeeRepository EmployeeRep { get; }

    }
}
