using Microsoft.AspNetCore.Authorization;

namespace Messaging.Platform.Api.Infrastructure.Auth;

public sealed class PlatformRoleAuthorizationHandler : AuthorizationHandler<PlatformRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformRoleRequirement requirement)
    {
        var role = context.User.FindFirst(AuthClaimTypes.Role)?.Value;
        if (string.IsNullOrWhiteSpace(role))
        {
            return Task.CompletedTask;
        }

        if (RoleRank(role) >= RoleRank(requirement.MinimumRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static int RoleRank(string role)
    {
        return role.ToLowerInvariant() switch
        {
            "viewer" => 1,
            "approver" => 2,
            "admin" => 3,
            _ => 0
        };
    }
}
