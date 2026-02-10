using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Messaging.Platform.Api.Infrastructure.Auth;

public static class GoogleJwtClaimNormalizer
{
    public static Task NormalizeAsync(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return Task.CompletedTask;
        }

        var subject = identity.FindFirstValue("sub");
        var email = identity.FindFirstValue("email");
        var displayName = identity.FindFirstValue("name") ?? email;

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            context.Fail("Google token missing required claims.");
            return Task.CompletedTask;
        }

        identity.AddClaim(new Claim(AuthClaimTypes.AuthProvider, "google"));
        identity.AddClaim(new Claim(AuthClaimTypes.AuthSubject, subject));
        identity.AddClaim(new Claim(AuthClaimTypes.Email, email));
        identity.AddClaim(new Claim(AuthClaimTypes.DisplayName, displayName ?? email));

        return Task.CompletedTask;
    }
}
