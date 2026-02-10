using Microsoft.AspNetCore.Authorization;

namespace Messaging.Platform.Api.Infrastructure.Auth;

public sealed class PlatformRoleRequirement : IAuthorizationRequirement
{
    public PlatformRoleRequirement(string minimumRole)
    {
        MinimumRole = minimumRole;
    }

    public string MinimumRole { get; }
}
