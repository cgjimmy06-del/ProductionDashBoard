namespace FProductionDashBoard.Services
{
    public class MultiCardReaderService : ICardReaderService, IDisposable
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
            reader.CardRead += OnCardRead;
            _readers.Add(reader);
            reader.Start();
            return reader;
        }
        public void RemoveReader(CardReaderService reader)
        {
            reader.CardRead -= OnCardRead;
            reader.Stop();
            _readers.Remove(reader);
        }

        private void OnCardRead(object? sender, CardReadEventArgs e) => CardRead?.Invoke(sender, e);

        public void Dispose()
        {
            Stop();
            foreach (var r in _readers)
                r.Dispose();
            _readers.Clear();
        }
    }
}
