using System.Diagnostics;
using System.IO.Ports;

namespace FProductionDashBoard.Services
{
    public class CardReaderService : ICardReaderService, IDisposable
    {
        public string PortName { get; private set; }
        public int BaudRate { get; private set; }

        private SerialPort? _port;
        private string _buffer = string.Empty;
        private string? _lastCardId;
        private readonly object _lock = new();

        public event EventHandler<CardReadEventArgs>? CardRead;
        public bool IsConnected => _port?.IsOpen == true;

        public CardReaderService(string portName = "COM3", int baudRate = 115200)
        {
            PortName = portName;
            BaudRate = baudRate;
        }

        public void Start()
        {
            try
            {
                _port = new SerialPort(PortName, BaudRate) { ReadTimeout = 500 };
                _port.DataReceived += OnDataReceived;
                _port.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CardReader] Failed to open {PortName}: {ex.Message}");
                _port?.Dispose();
                _port = null;
            }
        }
        public void Stop()
        {
            if (_port == null) return;
            _port.DataReceived -= OnDataReceived;
            try { if (_port.IsOpen) _port.Close(); } catch { }
            _port.Dispose();
            _port = null;
        }
        public void Restart(string portName, int baudRate)
        {
            PortName = portName;
            BaudRate = baudRate;
            Stop();
            Start();
        }
        public void ResetLastCard() { lock (_lock) { _lastCardId = null; } }
        public void Dispose() => Stop();

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var incoming = ((SerialPort)sender).ReadExisting();
                string? cardId;
                lock (_lock)
                {
                    _buffer += incoming;
                    cardId = TryExtractCardId(ref _buffer);
                }
                if (cardId != null && cardId != _lastCardId)
                {
                    _lastCardId = cardId;
                    CardRead?.Invoke(this, new CardReadEventArgs(cardId, PortName));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CardReader] DataReceived error: {ex.Message}");
            }
        }

        public static string? TryExtractCardId(ref string buffer)
        {
            int idx = buffer.IndexOfAny(new[] { '\r', '\n' });
            if (idx < 0) return null;

            var cardId = new string(buffer[..idx].Where(c => !char.IsControl(c)).ToArray()).Trim();
            buffer = buffer[(idx + 1)..].TrimStart('\r', '\n');

            return string.IsNullOrEmpty(cardId) ? null : cardId;
        }
    }
}
