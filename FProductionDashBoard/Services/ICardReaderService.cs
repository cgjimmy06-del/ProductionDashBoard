namespace FProductionDashBoard.Services
{
    public interface ICardReaderService
    {
        /// <summary>Fired on a ThreadPool thread; payload is the trimmed card ID string.</summary>
        event EventHandler<string>? CardRead;
        bool IsConnected { get; }
        void Start();
        void Stop();
    }
}
