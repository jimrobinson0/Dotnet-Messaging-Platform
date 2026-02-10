using Messaging.Platform.Api.Application.Messages;
using Messaging.Platform.Api.Contracts.Messages;
using Messaging.Platform.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Messaging.Platform.Api.Controllers;

[ApiController]
[Route("messages")]
public sealed class MessagesController : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";

    private readonly IMessageApplicationService _messageApplicationService;
    private readonly UserContextAccessor _userContextAccessor;

    public MessagesController(IMessageApplicationService messageApplicationService, UserContextAccessor userContextAccessor)
    {
        _messageApplicationService = messageApplicationService;
        _userContextAccessor = userContextAccessor;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Create(
        [FromBody] CreateMessageRequest request,
        [FromHeader(Name = IdempotencyKeyHeaderName)]
        string? idempotencyKeyHeader,
        CancellationToken cancellationToken)
    {
        if (Request.Headers.TryGetValue(IdempotencyKeyHeaderName, out var headerValues) && headerValues.Count > 1)
            throw new ArgumentException(
                $"Header '{IdempotencyKeyHeaderName}' must be supplied at most once.",
                nameof(idempotencyKeyHeader));

        var idempotencyKey = ResolveIdempotencyKey(idempotencyKeyHeader, request.IdempotencyKey);
        var userContext = RequireUserContext();
        var createResult =
            await _messageApplicationService.CreateAsync(request.ToCommand(idempotencyKey, userContext), cancellationToken);
        var response = createResult.Message.ToResponse();

        if (createResult.WasCreated) return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);

        return Ok(response);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.Approver)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Approve(
        [FromRoute] Guid id,
        [FromBody] ReviewMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userContext = RequireUserContext();
        var message = await _messageApplicationService.ApproveAsync(id, request.ToCommand(userContext), cancellationToken);
        return Ok(message.ToResponse());
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.Approver)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Reject(
        [FromRoute] Guid id,
        [FromBody] ReviewMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userContext = RequireUserContext();
        var message = await _messageApplicationService.RejectAsync(id, request.ToCommand(userContext), cancellationToken);
        return Ok(message.ToResponse());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Viewer)]
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
    [Authorize(Policy = AuthorizationPolicies.Viewer)]
    [ProducesResponseType(typeof(IReadOnlyList<MessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int limit = 50,
        [FromQuery] DateTimeOffset? createdAfter = null,
        CancellationToken cancellationToken = default)
    {
        var messages = await _messageApplicationService.ListAsync(status, limit, createdAfter, cancellationToken);
        return Ok(messages.Select(message => message.ToResponse()).ToArray());
    }

    private IUserContext RequireUserContext()
    {
        return _userContextAccessor.Current
               ?? throw new InvalidOperationException("User context is unavailable for authenticated request.");
    }

    private static string? ResolveIdempotencyKey(string? idempotencyKeyHeader, string? idempotencyKeyBody)
    {
        var hasHeader = !string.IsNullOrWhiteSpace(idempotencyKeyHeader);
        var hasBody = !string.IsNullOrWhiteSpace(idempotencyKeyBody);

        if (!hasHeader && !hasBody) return null;

        var normalizedHeader = hasHeader ? idempotencyKeyHeader!.Trim() : null;
        var normalizedBody = hasBody ? idempotencyKeyBody!.Trim() : null;

        if (hasHeader && hasBody && !string.Equals(normalizedHeader, normalizedBody, StringComparison.Ordinal))
            throw new ArgumentException("Idempotency key mismatch between header and body.");

        return normalizedHeader ?? normalizedBody;
    }
}
