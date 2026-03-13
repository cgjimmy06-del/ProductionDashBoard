using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Repositories
{
    public interface IDataService
    {


    }

    public class SqlService : IDataService
    {
        public IDeviceRepository DeviceRepo { get; }
        public IWorkerRepository WorkerRepo { get; }

        public SqlService(IDeviceRepository devicerepo, IWorkerRepository workerrepo) 
        {
            DeviceRepo = devicerepo;
            WorkerRepo = workerrepo;

        }

    }



}
