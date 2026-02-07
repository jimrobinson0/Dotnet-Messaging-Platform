using Messaging.Platform.Api.Application.Messages;
using Messaging.Platform.Api.Contracts.Messages;
using Microsoft.AspNetCore.Mvc;

namespace Messaging.Platform.Api.Controllers;

[ApiController]
[Route("messages")]
public sealed class MessagesController : ControllerBase
{
    private readonly IMessageApplicationService _messageApplicationService;

    public MessagesController(IMessageApplicationService messageApplicationService)
    {
        _messageApplicationService = messageApplicationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Create(
        [FromBody] CreateMessageRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _messageApplicationService.CreateAsync(request.ToCommand(), cancellationToken);
        var response = created.ToResponse();

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
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
}
