namespace Messaging.Platform.Persistence.Tests.Infrastructure;

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    // xUnit collection fixture marker.
}