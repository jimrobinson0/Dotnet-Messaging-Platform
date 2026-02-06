using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Messaging.Platform.Persistence.Tests.Infrastructure;

/// <summary>
/// Shared PostgreSQL container for persistence integration tests.
/// Applies schema migrations once per test run.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresFixture()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("messaging_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString() + ";Search Path=core";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        // Defensive: ensure schema exists even if something went sideways earlier
        await conn.ExecuteAsync("CREATE SCHEMA IF NOT EXISTS core;", cancellationToken);

        // Tests intentionally operate within the core schema
        await conn.ExecuteAsync("SET search_path TO core;", cancellationToken);

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string sql = """
        TRUNCATE TABLE
          message_audit_events,
          message_reviews,
          message_participants,
          messages
        RESTART IDENTITY CASCADE;
        """

        ;

        await conn.ExecuteAsync(sql, transaction: tx);
        await tx.CommitAsync(cancellationToken);
    }

    private async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        var migrationsDir = SchemaLocator.FindMigrationsDirectory();
        var migrationFiles = Directory.GetFiles(migrationsDir, "*.sql")
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (migrationFiles.Length == 0)
        {
            throw new InvalidOperationException(
                $"No .sql migration files found in '{migrationsDir}'.");
        }

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken);

        // IMPORTANT:
        // Migrations are schema-explicit and must not depend on search_path.
        foreach (var path in migrationFiles)
        {
            var sql = await File.ReadAllTextAsync(path, cancellationToken);

            if (string.IsNullOrWhiteSpace(sql))
            {
                continue;
            }

            await conn.ExecuteAsync(sql, cancellationToken);
        }
    }
}
