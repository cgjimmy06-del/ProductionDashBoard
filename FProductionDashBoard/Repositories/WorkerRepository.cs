using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace FProductionDashBoard.Repositories
{
    public class WorkerRepository : Repository<WorkerInfo>, IWorkerRepository
    {
        private readonly AppDbContext _context;

        public WorkerRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }



    }
}
