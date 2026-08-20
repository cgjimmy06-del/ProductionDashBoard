namespace FProductionDashBoard.Services;

public class DashboardCoreServices
{
    public LogService Log { get; }
    public IDataService Data { get; }
    public AuthorizationService Authorization { get; }
    public ICardReaderService CardReader { get; }
    public IWarehouseService Warehouse { get; }

    public DashboardCoreServices(LogService log, IDataService data,
        AuthorizationService auth, ICardReaderService cardReader,
        IWarehouseService warehouse)
    {
        Log = log;
        Data = data;
        Authorization = auth;
        CardReader = cardReader;
        Warehouse = warehouse;
    }
}
