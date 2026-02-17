namespace Messaging.Api.Infrastructure.Http;

internal sealed record ApiErrorResponse(
    string Error,
    string Message,
    object? Details = null);
