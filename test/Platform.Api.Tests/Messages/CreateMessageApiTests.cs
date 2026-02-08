using System.Net;
using System.Net.Http.Json;
using Messaging.Platform.Api.Application.Messages;
using Messaging.Platform.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Messaging.Platform.Api.Tests.Messages;

public sealed class CreateMessageApiTests
{
    [Fact]
    public async Task Create_uses_header_idempotency_key_and_returns_201_for_new_message()
    {
        var service = Substitute.For<IMessageApplicationService>();
        CreateMessageCommand? capturedCommand = null;
        var createdMessage = BuildMessage("header-key");

        service.CreateAsync(Arg.Do<CreateMessageCommand>(command => capturedCommand = command), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CreateMessageResult(createdMessage, WasCreated: true)));

        await using var factory = new MessagingApiFactory(service);
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
                participants = Array.Empty<object>(),
                actorType = "System",
                actorId = "api"
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "  header-key  ");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(capturedCommand);
        Assert.Equal("header-key", capturedCommand!.IdempotencyKey);
    }

    [Fact]
    public async Task Create_returns_200_for_replayed_request()
    {
        var service = Substitute.For<IMessageApplicationService>();
        var replayedMessage = BuildMessage("replay-key");

        service.CreateAsync(Arg.Any<CreateMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CreateMessageResult(replayedMessage, WasCreated: false)));

        await using var factory = new MessagingApiFactory(service);
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
                participants = Array.Empty<object>(),
                actorType = "System",
                actorId = "api"
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "replay-key");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_400_when_header_and_body_idempotency_keys_differ()
    {
        var service = Substitute.For<IMessageApplicationService>();

        await using var factory = new MessagingApiFactory(service);
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
                participants = Array.Empty<object>(),
                actorType = "System",
                actorId = "api"
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "header-key");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Idempotency key mismatch", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(
            service.ReceivedCalls(),
            call => string.Equals(call.GetMethodInfo().Name, nameof(IMessageApplicationService.CreateAsync), StringComparison.Ordinal));
    }

    private static Message BuildMessage(string idempotencyKey)
    {
        return Message.CreateApproved(
            id: Guid.NewGuid(),
            channel: "email",
            contentSource: MessageContentSource.Direct,
            templateKey: null,
            templateVersion: null,
            templateResolvedAt: null,
            subject: "Subject",
            textBody: "Body",
            htmlBody: null,
            templateVariables: null,
            idempotencyKey: idempotencyKey,
            participants: Array.Empty<MessageParticipant>());
    }

    private sealed class MessagingApiFactory : WebApplicationFactory<Program>
    {
        private readonly IMessageApplicationService _messageApplicationService;

        public MessagingApiFactory(IMessageApplicationService messageApplicationService)
        {
            _messageApplicationService = messageApplicationService;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Messaging"] =
                        "Host=localhost;Port=5432;Database=messaging_test;Username=postgres;Password=postgres"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMessageApplicationService>();
                services.AddSingleton(_messageApplicationService);
            });
        }
    }
}
