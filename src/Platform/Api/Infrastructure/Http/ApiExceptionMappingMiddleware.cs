using System.Text.Json;
using Messaging.Platform.Core.Exceptions;
using Messaging.Platform.Persistence.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Messaging.Platform.Api.Infrastructure.Http;

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
        catch (InvalidMessageStatusTransitionException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Invalid Transition", ex.Message);
        }
        catch (ApprovalRuleViolationException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Approval Rule Violation", ex.Message);
        }
        catch (ConcurrencyException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (JsonException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (PersistenceException ex)
        {
            _logger.LogError(ex, "Persistence failure while processing request.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Persistence failure",
                "A persistence error occurred.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing request.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}