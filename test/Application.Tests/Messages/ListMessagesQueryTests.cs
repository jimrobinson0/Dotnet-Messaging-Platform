using Messaging.Application.Messages;
using Messaging.Core.Exceptions;
using Messaging.Persistence.Messages.Reads;

namespace Messaging.Application.Tests.Messages;

public sealed class ListMessagesQueryTests
{
    [Fact]
    public async Task ListAsync_negative_page_throws_MessageValidationException()
    {
        var service = new MessageQueryService(new ThrowingReadRepository());

        var exception = await Assert.ThrowsAsync<MessageValidationException>(() =>
            service.ListAsync(new ListMessagesQuery
            {
                Page = -1,
                PageSize = 50,
                Status = []
            }));

        Assert.Equal("LIST_MESSAGES_INVALID_PAGE", exception.Code);
    }

    [Fact]
    public async Task ListAsync_negative_page_size_throws_MessageValidationException()
    {
        var service = new MessageQueryService(new ThrowingReadRepository());

        var exception = await Assert.ThrowsAsync<MessageValidationException>(() =>
            service.ListAsync(new ListMessagesQuery
            {
                Page = 1,
                PageSize = -1,
                Status = []
            }));

        Assert.Equal("LIST_MESSAGES_INVALID_PAGE_SIZE", exception.Code);
    }

    [Fact]
    public async Task ListAsync_page_size_above_max_throws_MessageValidationException()
    {
        var service = new MessageQueryService(new ThrowingReadRepository());

        var exception = await Assert.ThrowsAsync<MessageValidationException>(() =>
            service.ListAsync(new ListMessagesQuery
            {
                Page = 1,
                PageSize = 201,
                Status = []
            }));

        Assert.Equal("LIST_MESSAGES_INVALID_PAGE_SIZE", exception.Code);
    }

    private sealed class ThrowingReadRepository : IMessageReadRepository
    {
        public Task<PagedReadResult<MessageReadItem>> ListAsync(
            MessageReadQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Repository should not be called for invalid queries.");
        }
    }
}
