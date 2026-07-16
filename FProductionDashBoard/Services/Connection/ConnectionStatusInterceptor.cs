using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace FProductionDashBoard.Services
{
    public class ConnectionStatusInterceptor : DbConnectionInterceptor
    {
        private readonly ConnectionStatusService _status;

        public ConnectionStatusInterceptor(ConnectionStatusService status)
        {
            _status = status;
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            _status.Report(true);
            base.ConnectionOpened(connection, eventData);
        }

        public override Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _status.Report(true);
            return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
        {
            _status.Report(false);
            base.ConnectionFailed(connection, eventData);
        }

        public override Task ConnectionFailedAsync(
            DbConnection connection,
            ConnectionErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _status.Report(false);
            return base.ConnectionFailedAsync(connection, eventData, cancellationToken);
        }
    }
}
