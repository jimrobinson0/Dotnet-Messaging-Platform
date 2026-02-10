using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Messaging.Platform.Api.Infrastructure.Auth;

public sealed class DebugHeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DebugHeader";
    private const string UserHeader = "X-Debug-User";
    private const string RoleHeader = "X-Debug-Role";

    public DebugHeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Context.Request.Headers.TryGetValue(UserHeader, out var userValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var debugUser = userValues.FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(debugUser))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Header '{UserHeader}' must be non-empty."));
        }

        var debugRole = Context.Request.Headers.TryGetValue(RoleHeader, out var roleValues)
            ? roleValues.FirstOrDefault()?.Trim().ToLowerInvariant()
            : null;

        var claims = new List<Claim>
        {
            new(AuthClaimTypes.AuthProvider, "local"),
            new(AuthClaimTypes.AuthSubject, debugUser),
            new(AuthClaimTypes.Email, debugUser),
            new(AuthClaimTypes.DisplayName, debugUser)
        };

        if (!string.IsNullOrWhiteSpace(debugRole))
        {
            claims.Add(new Claim(AuthClaimTypes.DebugRole, debugRole));
            claims.Add(new Claim(AuthClaimTypes.Role, debugRole));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
