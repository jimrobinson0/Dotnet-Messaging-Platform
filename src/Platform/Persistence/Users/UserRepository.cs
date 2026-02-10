using Dapper;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Exceptions;
using Npgsql;

namespace Messaging.Platform.Persistence.Users;

public sealed class UserRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public UserRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PlatformUserRecord> UpsertHumanUserAsync(
        string authProvider,
        string authSubject,
        string email,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authProvider)) throw new ArgumentException("Value is required.", nameof(authProvider));
        if (string.IsNullOrWhiteSpace(authSubject)) throw new ArgumentException("Value is required.", nameof(authSubject));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Value is required.", nameof(email));

        const string sql = """
                           insert into users (
                             auth_provider,
                             auth_subject,
                             email,
                             display_name,
                             role,
                             is_active
                           )
                           values (
                             @AuthProvider,
                             @AuthSubject,
                             @Email,
                             @DisplayName,
                             'viewer'::messaging_user_role,
                             true
                           )
                           on conflict (auth_provider, auth_subject)
                           do update set
                             email = excluded.email,
                             display_name = excluded.display_name,
                             updated_at = now()
                           returning
                             id,
                             auth_provider as AuthProvider,
                             auth_subject as AuthSubject,
                             email,
                             display_name as DisplayName,
                             role::text as Role,
                             is_active as IsActive;
                           """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleAsync<PlatformUserRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        AuthProvider = authProvider.Trim(),
                        AuthSubject = authSubject.Trim(),
                        Email = email.Trim(),
                        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim()
                    },
                    cancellationToken: cancellationToken));

            return row.ToRecord();
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to upsert user.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to upsert user.", ex);
        }
    }

    public async Task<PlatformUserRecord?> GetByProviderSubjectAsync(
        string authProvider,
        string authSubject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authProvider)) throw new ArgumentException("Value is required.", nameof(authProvider));
        if (string.IsNullOrWhiteSpace(authSubject)) throw new ArgumentException("Value is required.", nameof(authSubject));

        const string sql = """
                           select
                             id,
                             auth_provider as AuthProvider,
                             auth_subject as AuthSubject,
                             email,
                             display_name as DisplayName,
                             role::text as Role,
                             is_active as IsActive
                           from users
                           where auth_provider = @AuthProvider
                             and auth_subject = @AuthSubject;
                           """;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<PlatformUserRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        AuthProvider = authProvider.Trim(),
                        AuthSubject = authSubject.Trim()
                    },
                    cancellationToken: cancellationToken));

            return row?.ToRecord();
        }
        catch (PostgresException ex)
        {
            throw new PersistenceException("Failed to load user.", ex);
        }
        catch (NpgsqlException ex)
        {
            throw new PersistenceException("Failed to load user.", ex);
        }
    }

    private sealed class PlatformUserRow
    {
        public Guid Id { get; init; }
        public string AuthProvider { get; init; } = string.Empty;
        public string AuthSubject { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        public string Role { get; init; } = string.Empty;
        public bool IsActive { get; init; }

        public PlatformUserRecord ToRecord()
        {
            return new PlatformUserRecord(
                Id,
                AuthProvider,
                AuthSubject,
                Email,
                DisplayName,
                Role,
                IsActive);
        }
    }
}
