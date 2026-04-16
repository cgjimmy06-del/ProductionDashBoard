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
        public DateTime BusinessDay { get; set; }
        public Task Demo();
        public IEquipmentRepository EquipmentRep { get; }
        public IEmployeeRepository EmployeeRep { get; }
        public IMaterialRepository MaterialRep { get; }
        public IErrorListRepository ErrorListRep { get; }
        public IMaterialReplacementRepository MaterialReplacementRep { get; }
        public IInspectionRecordRepository InspectionRecordRep { get; }

    }
}
