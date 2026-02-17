using System.Text.Json;
using Messaging.Api.Exceptions;
using Messaging.Core.Exceptions;
using Messaging.Persistence.Exceptions;

namespace Messaging.Api.Infrastructure.Http;

public sealed class ApiExceptionMappingMiddleware
{
    private readonly ILogger<ApiExceptionMappingMiddleware> _logger;
    private readonly RequestDelegate _next;

    public ApiExceptionMappingMiddleware(
        RequestDelegate next,
        ILogger<ApiExceptionMappingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiContractException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ex.ErrorCode, ex.Message);
        }
        catch (MessageValidationException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ex.Code, ex.Message);
        }
        catch (MessageConflictException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, ex.Code, ex.Message);
        }
        catch (InvalidMessageStatusTransitionException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, "INVALID_MESSAGE_STATUS_TRANSITION", ex.Message);
        }
        catch (ApprovalRuleViolationException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, "APPROVAL_RULE_VIOLATION", ex.Message);
        }
        catch (ConcurrencyException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, "CONFLICT", ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "NOT_FOUND", ex.Message);
        }
        catch (JsonException)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "INVALID_JSON", "Invalid JSON payload.");
        }
        catch (PersistenceException ex)
        {
            _logger.LogError(ex, "Persistence failure while processing request.");
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "PERSISTENCE_FAILURE",
                "A persistence error occurred.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing request.");
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string errorCode,
        string message)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new ApiErrorResponse(errorCode, message));
    }
}
