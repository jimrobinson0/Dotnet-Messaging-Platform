namespace Messaging.Persistence.Tests.Infrastructure;

/// <summary>
///     Base class for tests that require a clean database.
/// </summary>
[Collection("Postgres")]
public abstract class PostgresTestBase(PostgresFixture fixture)
{
    protected PostgresFixture Fixture { get; } = fixture;

    protected Task ResetDbAsync()
    {
        return Fixture.ResetDatabaseAsync();
    }
}