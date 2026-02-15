using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Messaging.Api.Application.Messages;
using Messaging.Core;
using Messaging.Core.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Messaging.Api.Tests.Messages;

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
    [Trait("Category", "Contract")]
    public async Task Create_returns_200_for_replayed_request()
    {
        var service = Substitute.For<IMessageApplicationService>();
        var replayedMessage = BuildMessage("replay-key");

        service.CreateAsync(Arg.Any<CreateMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CreateMessageResult(replayedMessage, false)));

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
    [Trait("Category", "Contract")]
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
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid request", root.GetProperty("title").GetString());
        Assert.Equal("IDEMPOTENCY_KEY_MISMATCH", root.GetProperty("code").GetString());
        Assert.Contains("Idempotency key mismatch", root.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            service.ReceivedCalls(),
            call => string.Equals(call.GetMethodInfo().Name, nameof(IMessageApplicationService.CreateAsync),
                StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Create_maps_reply_to_message_id_to_command()
    {
        var service = Substitute.For<IMessageApplicationService>();
        CreateMessageCommand? capturedCommand = null;
        var createdMessage = BuildMessage("header-key");
        var replyToMessageId = Guid.NewGuid();

        service.CreateAsync(Arg.Do<CreateMessageCommand>(command => capturedCommand = command),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CreateMessageResult(createdMessage, true)));

        await using var factory = new MessagingApiFactory(service);
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/messages")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["channel"] = "email",
                ["contentSource"] = "Direct",
                ["requiresApproval"] = false,
                ["subject"] = "Subject",
                ["textBody"] = "Hello",
                ["replyToMessageId"] = replyToMessageId,
                ["participants"] = Array.Empty<object>(),
                ["actorType"] = "System",
                ["actorId"] = "api"
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "header-key");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(capturedCommand);
        Assert.Equal(replyToMessageId, capturedCommand!.ReplyToMessageId);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Create_returns_400_with_invalid_reply_target_code()
    {
        var service = Substitute.For<IMessageApplicationService>();
        service.CreateAsync(Arg.Any<CreateMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CreateMessageResult>(
                new MessageValidationException("INVALID_REPLY_TARGET", "Invalid reply target")));

        var replyToMessageId = Guid.NewGuid();

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
                replyToMessageId = replyToMessageId,
                participants = Array.Empty<object>(),
                actorType = "System",
                actorId = "api"
            })
        };

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid request", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("Invalid reply target", root.GetProperty("detail").GetString());
        Assert.Equal("INVALID_REPLY_TARGET", root.GetProperty("code").GetString());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task GetById_returns_reply_metadata_fields_when_present()
    {
        var service = Substitute.For<IMessageApplicationService>();
        var replyToMessageId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        const string inReplyTo = "<parent@example.test>";
        const string referencesHeader = "<root@example.test> <parent@example.test>";
        const string smtpMessageId = "<child@example.test>";

        var message = BuildMessage(
            idempotencyKey: "reply-view-key",
            id: messageId,
            replyToMessageId: replyToMessageId,
            inReplyTo: inReplyTo,
            referencesHeader: referencesHeader,
            smtpMessageId: smtpMessageId);

        service.GetByIdAsync(messageId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(message));

        await using var factory = new MessagingApiFactory(service);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/messages/{messageId}");
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(replyToMessageId.ToString(), root.GetProperty("reply_to_message_id").GetString());
        Assert.Equal(inReplyTo, root.GetProperty("in_reply_to").GetString());
        Assert.Equal(referencesHeader, root.GetProperty("references_header").GetString());
        Assert.Equal(smtpMessageId, root.GetProperty("smtp_message_id").GetString());
    }

    private static Message BuildMessage(
        string idempotencyKey,
        Guid? id = null,
        Guid? replyToMessageId = null,
        string? inReplyTo = null,
        string? referencesHeader = null,
        string? smtpMessageId = null)
    {
        return Message.CreateApproved(
            id ?? Guid.NewGuid(),
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
            Array.Empty<MessageParticipant>(),
            replyToMessageId,
            inReplyTo,
            referencesHeader,
            smtpMessageId);
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
                    ["ConnectionStrings:Messaging"] = "FakeConnectionString"
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
