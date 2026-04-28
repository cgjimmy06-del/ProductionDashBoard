namespace FProductionDashBoard.Services
{
    public class CardReadEventArgs : EventArgs
    {
        public string CardId { get; }
        public string PortName { get; }

        public CardReadEventArgs(string cardId, string portName)
        {
            CardId = cardId;
            PortName = portName;
        }
    }
}
