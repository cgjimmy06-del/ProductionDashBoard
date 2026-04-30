namespace FProductionDashBoard.Services
{
    public interface ICardReaderService
    {
        /// <summary>Fired on a ThreadPool thread; payload contains cardId and source port name.</summary>
        event EventHandler<CardReadEventArgs>? CardRead;
        bool IsConnected { get; }
        void Start();
        void Stop();
        void ResetLastCard();
    }
}
