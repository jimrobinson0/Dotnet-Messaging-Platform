using System.Net;
using System.Net.Http.Json;
using Messaging.Platform.Api.Application.Messages;
using Messaging.Platform.Api.Infrastructure.Auth;
using Messaging.Platform.Core;
using Messaging.Platform.Persistence.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Messaging.Platform.Api.Tests.Messages;

public sealed class CreateMessageApiContractTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public async Task Create_uses_header_idempotency_key_and_returns_201_for_new_message()
    {
        var service = Substitute.For<IMessageApplicationService>();
        CreateMessageCommand? capturedCommand = null;
        var createdMessage = BuildMessage("header-key");

        service.CreateAsync(Arg.Do<CreateMessageCommand>(command => capturedCommand = command),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CreateMessageResult(createdMessage, true)));

        var resolver = BuildResolver();

        await using var factory = new MessagingApiFactory(service, resolver);
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/messages")
        {
            Content = JsonContent.Create(new
            {
                channel = "email",
                contentSource = "Direct",
                requiresApproval = false,
                subject = "Subject",
                textBody = "Hello",
                participants = Array.Empty<object>()
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "  header-key  ");
        request.Headers.TryAddWithoutValidation("X-Debug-User", "admin@local.dev");
        request.Headers.TryAddWithoutValidation("X-Debug-Role", "admin");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(capturedCommand);
        Assert.Equal("header-key", capturedCommand!.IdempotencyKey);
        Assert.NotNull(capturedCommand.ActorUserId);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Create_returns_200_for_replayed_request()
    {
        var service = Substitute.For<IMessageApplicationService>();
        var replayedMessage = BuildMessage("replay-key");

        service.CreateAsync(Arg.Any<CreateMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CreateMessageResult(replayedMessage, false)));

        await using var factory = new MessagingApiFactory(service, BuildResolver());
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/messages")
        {
            Content = JsonContent.Create(new
            {
                channel = "email",
                contentSource = "Direct",
                requiresApproval = false,
                subject = "Subject",
                textBody = "Hello",
                participants = Array.Empty<object>()
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "replay-key");
        request.Headers.TryAddWithoutValidation("X-Debug-User", "admin@local.dev");
        request.Headers.TryAddWithoutValidation("X-Debug-Role", "admin");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Create_returns_400_when_header_and_body_idempotency_keys_differ()
    {
        var service = Substitute.For<IMessageApplicationService>();

        await using var factory = new MessagingApiFactory(service, BuildResolver());
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/messages")
        {
            Content = JsonContent.Create(new
            {
                channel = "email",
                contentSource = "Direct",
                requiresApproval = false,
                subject = "Subject",
                textBody = "Hello",
                idempotencyKey = "body-key",
                participants = Array.Empty<object>()
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "header-key");
        request.Headers.TryAddWithoutValidation("X-Debug-User", "admin@local.dev");
        request.Headers.TryAddWithoutValidation("X-Debug-Role", "admin");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Idempotency key mismatch", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(
            service.ReceivedCalls(),
            call => string.Equals(call.GetMethodInfo().Name, nameof(IMessageApplicationService.CreateAsync),
                StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Get_requires_viewer_policy()
    {
        var service = Substitute.For<IMessageApplicationService>();
        service.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(BuildMessage(null));

        await using var factory = new MessagingApiFactory(service, BuildResolver());
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/messages/{Guid.NewGuid()}");
        request.Headers.TryAddWithoutValidation("X-Debug-User", "viewer@local.dev");
        request.Headers.TryAddWithoutValidation("X-Debug-Role", "viewer");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Create_rejects_viewer_role()
    {
        var service = Substitute.For<IMessageApplicationService>();

        await using var factory = new MessagingApiFactory(service, BuildResolver());
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/messages")
        {
            Content = JsonContent.Create(new
            {
                channel = "email",
                contentSource = "Direct",
                requiresApproval = false,
                subject = "Subject",
                textBody = "Hello",
                participants = Array.Empty<object>()
            })
        };
        request.Headers.TryAddWithoutValidation("X-Debug-User", "viewer@local.dev");
        request.Headers.TryAddWithoutValidation("X-Debug-Role", "viewer");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static IUserResolver BuildResolver()
    {
        var resolver = Substitute.For<IUserResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var email = call.ArgAt<string>(2);
                return new PlatformUserRecord(
                    Guid.NewGuid(),
                    call.ArgAt<string>(0),
                    call.ArgAt<string>(1),
                    email,
                    call.ArgAt<string?>(3),
                    "viewer",
                    true);
            });
        return resolver;
    }

    private static Message BuildMessage(string? idempotencyKey)
    {
        return Message.CreateApproved(
            Guid.NewGuid(),
            "email",
            MessageContentSource.Direct,
            null,
            null,
            null,
            "Subject",
            "Body",
            null,
            null,
            idempotencyKey,
            Array.Empty<MessageParticipant>());
    }

    private sealed class MessagingApiFactory : WebApplicationFactory<Program>
    {
        private readonly IMessageApplicationService _messageApplicationService;
        private readonly IUserResolver _userResolver;

        public MessagingApiFactory(IMessageApplicationService messageApplicationService, IUserResolver userResolver)
        {
            _messageApplicationService = messageApplicationService;
            _userResolver = userResolver;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
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
                services.RemoveAll<IUserResolver>();
                services.AddSingleton(_messageApplicationService);
                services.AddSingleton(_userResolver);
            });
        }
    }
}
