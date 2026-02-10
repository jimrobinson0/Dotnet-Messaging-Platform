using System.Security.Claims;
namespace Messaging.Platform.Api.Infrastructure.Auth;

public sealed class UserResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public UserResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserResolver userResolver, UserContextAccessor userContextAccessor)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var authProvider = principal.FindFirstValue(AuthClaimTypes.AuthProvider);
        var authSubject = principal.FindFirstValue(AuthClaimTypes.AuthSubject);
        var email = principal.FindFirstValue(AuthClaimTypes.Email);
        var displayName = principal.FindFirstValue(AuthClaimTypes.DisplayName);

        if (string.IsNullOrWhiteSpace(authProvider) || string.IsNullOrWhiteSpace(authSubject) || string.IsNullOrWhiteSpace(email))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Authenticated principal is missing required identity claims.");
            return;
        }

        var user = await userResolver.ResolveAsync(authProvider, authSubject, email, displayName, context.RequestAborted);

        if (!user.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("User is inactive.");
            return;
        }

        var effectiveRole = user.Role;
        if (context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            var debugRole = principal.FindFirstValue(AuthClaimTypes.DebugRole);
            if (!string.IsNullOrWhiteSpace(debugRole))
            {
                effectiveRole = debugRole;
            }
        }

        var identity = principal.Identities.First();
        var existingRoleClaims = identity.FindAll(AuthClaimTypes.Role).ToArray();
        foreach (var claim in existingRoleClaims)
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(AuthClaimTypes.Role, effectiveRole));

        userContextAccessor.Current = new ResolvedUserContext(user.Id, effectiveRole, user.Email, user.DisplayName);

        await _next(context);
    }
}
