using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Messaging.Api.Application.Messages;
using Messaging.Api.Contracts;
using Messaging.Api.Contracts.Messages;
using Messaging.Core;
using Messaging.Persistence.Messages;
using Messaging.Persistence.Messages.Reads;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Messaging.Api.Tests.Messages;

public sealed class MessageListTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_filters_by_single_status()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();

        readRepository.ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(EmptyPage()));

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/messages?status=Failed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await readRepository.Received(1).ListAsync(
            Arg.Is<MessageReadQuery>(query =>
                query.Status != null &&
                query.Status.Count == 1 &&
                query.Status.Contains(MessageStatus.Failed)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_filters_by_multiple_statuses()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();

        readRepository.ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(EmptyPage()));

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/messages?status=Approved&status=Failed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await readRepository.Received(1).ListAsync(
            Arg.Is<MessageReadQuery>(query =>
                query.Status != null &&
                query.Status.Count == 2 &&
                query.Status.Contains(MessageStatus.Approved) &&
                query.Status.Contains(MessageStatus.Failed)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_pages_without_overlap()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();
        var pageOneIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002")
        };
        var pageTwoIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000004")
        };

        readRepository.ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<MessageReadQuery>();
                var ids = query.Page == 1 ? pageOneIds : pageTwoIds;

                return Task.FromResult(new PagedReadResult<MessageReadItem>
                {
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = 4,
                    Items = ids.Select(id => new MessageReadItem
                    {
                        Id = id,
                        Channel = "email",
                        Status = MessageStatus.Approved,
                        RequiresApproval = false,
                        CreatedAt = DateTimeOffset.UtcNow
                    }).ToArray()
                });
            });

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var page1 = await client.GetFromJsonAsync<PagedResultResponse<MessageSummaryResponse>>(
            "/messages?status=Approved&page=1&pageSize=2",
            JsonOptions);
        var page2 = await client.GetFromJsonAsync<PagedResultResponse<MessageSummaryResponse>>(
            "/messages?status=Approved&page=2&pageSize=2",
            JsonOptions);

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        var overlap = page1!.Items.Select(item => item.Id).Intersect(page2!.Items.Select(item => item.Id)).ToArray();
        Assert.Empty(overlap);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_allows_page_size_bounds_1_and_200()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();

        readRepository.ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(EmptyPage()));

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var oneResponse = await client.GetAsync("/messages?status=Approved&pageSize=1");
        var twoHundredResponse = await client.GetAsync("/messages?status=Approved&pageSize=200");

        Assert.Equal(HttpStatusCode.OK, oneResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, twoHundredResponse.StatusCode);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_returns_400_when_page_is_less_than_1()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/messages?status=Approved&page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await readRepository.DidNotReceive().ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_returns_400_for_invalid_page_size_bounds()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var zeroResponse = await client.GetAsync("/messages?status=Approved&pageSize=0");
        var tooLargeResponse = await client.GetAsync("/messages?status=Approved&pageSize=201");

        Assert.Equal(HttpStatusCode.BadRequest, zeroResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLargeResponse.StatusCode);
        await readRepository.DidNotReceive().ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_validates_time_range_edges()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();

        readRepository.ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(EmptyPage()));

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var createdAt = "2026-01-01T00:00:00Z";

        var validResponse = await client.GetAsync($"/messages?createdFrom={createdAt}&createdTo={createdAt}");
        var invalidCreatedResponse = await client.GetAsync(
            "/messages?createdFrom=2026-01-02T00:00:00Z&createdTo=2026-01-01T00:00:00Z");
        var invalidSentResponse = await client.GetAsync(
            "/messages?sentFrom=2026-01-02T00:00:00Z&sentTo=2026-01-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreatedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSentResponse.StatusCode);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_returns_400_when_required_filters_missing()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/messages?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await readRepository.DidNotReceive().ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_returns_items_in_deterministic_order_from_query_service()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();

        var first = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000009");

        readRepository.ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedReadResult<MessageReadItem>
            {
                Page = 1,
                PageSize = 50,
                TotalCount = 2,
                Items =
                [
                    new MessageReadItem
                    {
                        Id = first,
                        Channel = "email",
                        Status = MessageStatus.Approved,
                        RequiresApproval = false,
                        CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
                    },
                    new MessageReadItem
                    {
                        Id = second,
                        Channel = "email",
                        Status = MessageStatus.Approved,
                        RequiresApproval = false,
                        CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
                    }
                ]
            }));

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<PagedResultResponse<MessageSummaryResponse>>(
            "/messages?status=Approved",
            JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(first, payload!.Items[0].Id);
        Assert.Equal(second, payload.Items[1].Id);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task List_reports_total_count()
    {
        var appService = Substitute.For<IMessageApplicationService>();
        var readRepository = Substitute.For<IMessageReadRepository>();

        readRepository.ListAsync(Arg.Any<MessageReadQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedReadResult<MessageReadItem>
            {
                Page = 1,
                PageSize = 50,
                TotalCount = 42,
                Items = Array.Empty<MessageReadItem>()
            }));

        await using var factory = new MessagingApiFactory(appService, readRepository);
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<PagedResultResponse<MessageSummaryResponse>>(
            "/messages?status=Approved",
            JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(42, payload!.TotalCount);
    }

    private static PagedReadResult<MessageReadItem> EmptyPage()
    {
        return new PagedReadResult<MessageReadItem>
        {
            Page = 1,
            PageSize = 50,
            TotalCount = 0,
            Items = Array.Empty<MessageReadItem>()
        };
    }

    private sealed class MessagingApiFactory : WebApplicationFactory<Program>
    {
        private readonly IMessageApplicationService _applicationService;
        private readonly IMessageReadRepository _readRepository;

        public MessagingApiFactory(IMessageApplicationService applicationService, IMessageReadRepository readRepository)
        {
            _applicationService = applicationService;
            _readRepository = readRepository;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Messaging"] = "FakeConnectionString"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMessageApplicationService>();
                services.RemoveAll<IMessageReadRepository>();
                services.RemoveAll<IMessageQueryService>();

                services.AddSingleton(_applicationService);
                services.AddSingleton(_readRepository);
                services.AddScoped<IMessageQueryService, MessageQueryService>();
            });
        }
    }
}
