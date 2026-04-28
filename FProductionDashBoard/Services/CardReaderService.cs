using System.Diagnostics;
using System.IO.Ports;

namespace FProductionDashBoard.Services
{
    public class CardReaderService : ICardReaderService, IDisposable
    {
        // Phase 2: 改為從 Settings 讀取
        private const string PortName = "COM3";
        private const int BaudRate = 115200;

        private SerialPort? _port;
        private string _buffer = string.Empty;
        private string? _lastCardId;
        private readonly object _lock = new();

        public event EventHandler<string>? CardRead;
        public bool IsConnected => _port?.IsOpen == true;

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
                    CardRead?.Invoke(this, cardId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CardReader] DataReceived error: {ex.Message}");
            }
        }

        /// <summary>
        /// 從累積 buffer 中提取一筆卡號（以 CR 或 LF 分隔）。
        /// buffer 會在提取後就地更新，未完成的資料保留。
        /// </summary>
        public static string? TryExtractCardId(ref string buffer)
        {
            int idx = buffer.IndexOfAny(new[] { '\r', '\n' });
            if (idx < 0) return null;

            var cardId = buffer[..idx].Trim();
            buffer = buffer[(idx + 1)..].TrimStart('\r', '\n');

            return string.IsNullOrEmpty(cardId) ? null : cardId;
        }
    }
}
