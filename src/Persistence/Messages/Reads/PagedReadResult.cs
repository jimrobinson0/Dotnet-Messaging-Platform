namespace Messaging.Persistence.Messages.Reads;

public sealed class PagedReadResult<T>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyCollection<T> Items { get; init; } = Array.Empty<T>();
}
