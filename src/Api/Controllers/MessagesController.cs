using Messaging.Api.Contracts;
using Messaging.Api.Contracts.Messages;
using Messaging.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;
using ApplicationMessages = Messaging.Application.Messages;

namespace Messaging.Api.Controllers;

[ApiController]
[Route("messages")]
public sealed class MessagesController : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";

    private readonly ApplicationMessages.IMessageApplicationService _messageApplicationService;
    private readonly ApplicationMessages.IMessageQueryService _messageQueryService;

    public MessagesController(
        ApplicationMessages.IMessageApplicationService messageApplicationService,
        ApplicationMessages.IMessageQueryService messageQueryService)
    {
        _messageApplicationService = messageApplicationService;
        _messageQueryService = messageQueryService;
    }

    [HttpPost]
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
            throw new ApiContractException(
                "MULTIPLE_IDEMPOTENCY_HEADERS",
                $"Header '{IdempotencyKeyHeaderName}' must be supplied at most once.");

        var idempotencyKey = ResolveIdempotencyKey(idempotencyKeyHeader, request.IdempotencyKey);
        ApplicationMessages.CreateMessageResult createResult =
            await _messageApplicationService.CreateAsync(request.ToCommand(idempotencyKey), cancellationToken);
        var response = createResult.Message.ToResponse();

        if (createResult.Outcome == ApplicationMessages.IdempotencyOutcome.Created)
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);

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
    [ProducesResponseType(typeof(PagedResultResponse<MessageSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    // NOTE: This endpoint is the canonical and only list surface.
    // No backward-compatible overloads will be introduced.
    public async Task<ActionResult<PagedResultResponse<MessageSummaryResponse>>> List(
        [FromQuery] ListMessagesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _messageQueryService.ListAsync(query.ToApplicationQuery(), cancellationToken);
        return Ok(result.ToResponse());
    }

    private static string ResolveIdempotencyKey(string? headerKey, string? bodyKey)
    {
        var normalizedHeader = NormalizeIdempotencyKey(headerKey);
        var normalizedBody = NormalizeIdempotencyKey(bodyKey);

        if (normalizedHeader is null && normalizedBody is null)
            throw new ApiContractException(
                "IDEMPOTENCY_KEY_REQUIRED",
                "An idempotency key must be supplied via the 'Idempotency-Key' header or request body.");

        // Header/body mismatch (case-sensitive Ordinal)
        if (normalizedHeader is not null &&
            normalizedBody is not null &&
            !string.Equals(normalizedHeader, normalizedBody, StringComparison.Ordinal))
            throw new ApiContractException(
                "IDEMPOTENCY_KEY_MISMATCH",
                "Idempotency key mismatch: header and body must match exactly when both provided.");

        return normalizedHeader ?? normalizedBody!;
    }

    private static string? NormalizeIdempotencyKey(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
