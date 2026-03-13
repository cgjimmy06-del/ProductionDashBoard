using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace FProductionDashBoard.Repositories
{
    public class WorkerRepository : Repository<WorkerInfo, InfoDbContext>, IWorkerRepository
    {
        private readonly InfoDbContext _context;

        public WorkerRepository(InfoDbContext context) : base(context)
        {
            _context = context;
        }



    }
}
