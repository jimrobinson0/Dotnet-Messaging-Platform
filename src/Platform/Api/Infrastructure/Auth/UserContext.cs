namespace Messaging.Platform.Api.Infrastructure.Auth;

public interface IUserContext
{
    Guid UserId { get; }
    string Role { get; }
    string Email { get; }
    string? DisplayName { get; }
}

public sealed record ResolvedUserContext(
    Guid UserId,
    string Role,
    string Email,
    string? DisplayName) : IUserContext;

public sealed class UserContextAccessor
{
    public IUserContext? Current { get; set; }
}
