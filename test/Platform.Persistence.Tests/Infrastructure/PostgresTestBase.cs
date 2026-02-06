using System.Threading.Tasks;
using Xunit;

namespace Messaging.Platform.Persistence.Tests.Infrastructure;

/// <summary>
/// Base class for tests that require a clean database.
/// </summary>
[Collection("Postgres")]
public abstract class PostgresTestBase
{
    protected PostgresTestBase(PostgresFixture fixture)
    {
        Fixture = fixture;
    }

    protected PostgresFixture Fixture { get; }

    protected Task ResetDbAsync() => Fixture.ResetDatabaseAsync();
}
