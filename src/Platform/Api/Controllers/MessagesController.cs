using Messaging.Platform.Api.Application.Messages;
using Messaging.Platform.Api.Contracts.Messages;
using Microsoft.AspNetCore.Mvc;

namespace Messaging.Platform.Api.Controllers;

[ApiController]
[Route("messages")]
public sealed class MessagesController : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";

    private readonly IMessageApplicationService _messageApplicationService;

    public MessagesController(IMessageApplicationService messageApplicationService)
    {
        _messageApplicationService = messageApplicationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Create(
        [FromBody] CreateMessageRequest request,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKeyHeader,
        CancellationToken cancellationToken)
    {
        if (Request.Headers.TryGetValue(IdempotencyKeyHeaderName, out var headerValues) && headerValues.Count > 1)
        {
            throw new ArgumentException(
                $"Header '{IdempotencyKeyHeaderName}' must be supplied at most once.",
                nameof(idempotencyKeyHeader));
        }

        var idempotencyKey = ResolveIdempotencyKey(idempotencyKeyHeader, request.IdempotencyKey);
        var createResult = await _messageApplicationService.CreateAsync(request.ToCommand(idempotencyKey), cancellationToken);
        var response = createResult.Message.ToResponse();

        if (createResult.WasCreated)
        {
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        return Ok(response);
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Approve(
        [FromRoute] Guid id,
        [FromBody] ReviewMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = await _messageApplicationService.ApproveAsync(id, request.ToCommand(), cancellationToken);
        return Ok(message.ToResponse());
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Reject(
        [FromRoute] Guid id,
        [FromBody] ReviewMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = await _messageApplicationService.RejectAsync(id, request.ToCommand(), cancellationToken);
        return Ok(message.ToResponse());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var message = await _messageApplicationService.GetByIdAsync(id, cancellationToken);
        return Ok(message.ToResponse());
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int limit = 50,
        [FromQuery] DateTimeOffset? createdAfter = null,
        CancellationToken cancellationToken = default)
    {
        var messages = await _messageApplicationService.ListAsync(status, limit, createdAfter, cancellationToken);
        var response = messages.Select(message => message.ToResponse()).ToArray();

        return Ok(response);
    }

    private static string? ResolveIdempotencyKey(string? headerKey, string? bodyKey)
    {
        var normalizedHeader = NormalizeIdempotencyKey(headerKey);
        var normalizedBody = NormalizeIdempotencyKey(bodyKey);

        if (normalizedHeader is not null &&
            normalizedBody is not null &&
            !string.Equals(normalizedHeader, normalizedBody, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Idempotency key mismatch: header 'Idempotency-Key' and body 'idempotencyKey' must match when both are provided.");
        }

        return normalizedHeader ?? normalizedBody;
    }

    private static string? NormalizeIdempotencyKey(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length == 0 ? null : normalized;
    }
}
