namespace Messaging.Platform.Persistence.Users;

public sealed record PlatformUserRecord(
    Guid Id,
    string AuthProvider,
    string AuthSubject,
    string Email,
    string? DisplayName,
    string Role,
    bool IsActive);
