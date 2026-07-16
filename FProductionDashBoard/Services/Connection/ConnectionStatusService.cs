using System;

namespace FProductionDashBoard.Services
{
    public class ConnectionStatusService
    {
        private readonly object _lock = new();
        private bool? _state = null;
        private DateTime? _lastChangedAt;

        public bool IsConnected { get { lock (_lock) { return _state == true; } } }
        public DateTime? LastChangedAt { get { lock (_lock) { return _lastChangedAt; } } }

        public event EventHandler? StatusChanged;

        public void Report(bool connected)
        {
            bool shouldRaise = false;
            lock (_lock)
            {
                if (_state != connected)
                {
                    _state = connected;
                    _lastChangedAt = DateTime.Now;
                    shouldRaise = true;
                }
            }
            if (shouldRaise)
                StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        // 供需要同時讀取 IsConnected + LastChangedAt 的呼叫端（如 tooltip 組字串）取用同一時間點的一致快照，
        // 避免分兩次個別讀取時，中間被另一執行緒的 Report() 翻轉導致兩值配對不一致
        public (bool IsConnected, DateTime? LastChangedAt) GetSnapshot()
        {
            lock (_lock)
            {
                return (_state == true, _lastChangedAt);
            }
        }
    }
}
