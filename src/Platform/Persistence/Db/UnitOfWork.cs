using System.Data;
using System.Data.Common;
using Npgsql;

namespace Messaging.Platform.Persistence.Db;

public sealed class UnitOfWork : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _completed;

    private UnitOfWork(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public DbTransaction Transaction => _transaction;
    public DbConnection Connection => _connection;

    public static async Task<UnitOfWork> BeginAsync(
        DbConnectionFactory connectionFactory,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);
        return new UnitOfWork(connection, transaction);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        _completed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_completed)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_completed)
        {
            try
            {
                await _transaction.RollbackAsync();
            }
            catch
            {
                // Swallow rollback exceptions during dispose.
            }
        }

        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
