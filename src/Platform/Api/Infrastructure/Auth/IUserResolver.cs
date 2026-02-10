using Messaging.Platform.Persistence.Users;

namespace Messaging.Platform.Api.Infrastructure.Auth;

public interface IUserResolver
{
    Task<PlatformUserRecord> ResolveAsync(
        string authProvider,
        string authSubject,
        string email,
        string? displayName,
        CancellationToken cancellationToken = default);
}

public sealed class UserResolver : IUserResolver
{
    private readonly UserRepository _userRepository;

    public UserResolver(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PlatformUserRecord> ResolveAsync(
        string authProvider,
        string authSubject,
        string email,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        return await _userRepository.UpsertHumanUserAsync(authProvider, authSubject, email, displayName, cancellationToken);
    }
}
