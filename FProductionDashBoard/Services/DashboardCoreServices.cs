namespace FProductionDashBoard.Services;

public class DashboardCoreServices
{
    public LogService Log { get; }
    public IDataService Data { get; }
    public AuthorizationService Authorization { get; }
    public ICardReaderService CardReader { get; }

    public DashboardCoreServices(LogService log, IDataService data,
        AuthorizationService auth, ICardReaderService cardReader)
    {
        Log = log;
        Data = data;
        Authorization = auth;
        CardReader = cardReader;
    }
}
