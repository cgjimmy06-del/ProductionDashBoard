namespace FProductionDashBoard.Services.Exceptions
{
    public class DatabaseConnectionException : Exception
    {
        public DatabaseConnectionException(string message, Exception? innerException = null)
            : base(message, innerException) { }
    }
}
