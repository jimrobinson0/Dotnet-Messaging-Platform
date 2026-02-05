using System.Data.Common;
using Npgsql;

namespace Messaging.Platform.Persistence.Db;

internal static class DbGuard
{
    public static NpgsqlConnection GetConnection(DbTransaction transaction)
    {
        if (transaction.Connection is null)
        {
            throw new InvalidOperationException(
                "Transaction is not associated with an open connection.");
        }

        return (NpgsqlConnection)transaction.Connection;
    }
}
