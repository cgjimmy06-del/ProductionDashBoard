namespace FProductionDashBoard.Services
{
    public class MultiCardReaderService : ICardReaderService
    {
        private readonly List<CardReaderService> _readers = new();

        public event EventHandler<CardReadEventArgs>? CardRead;
        public bool IsConnected => _readers.Any(r => r.IsConnected);
        public IReadOnlyList<CardReaderService> Readers => _readers.AsReadOnly();

        public void Start() => _readers.ForEach(r => r.Start());
        public void Stop() => _readers.ForEach(r => r.Stop());
        public void ResetLastCard() => _readers.ForEach(r => r.ResetLastCard());

        public CardReaderService AddReader(string portName, int baudRate)
        {
            var reader = new CardReaderService(portName, baudRate);
            reader.CardRead += (s, e) => CardRead?.Invoke(s, e);
            _readers.Add(reader);
            reader.Start();
            return reader;
        }
        public void RemoveReader(CardReaderService reader)
        {
            reader.Stop();
            _readers.Remove(reader);
        }
    }
}
