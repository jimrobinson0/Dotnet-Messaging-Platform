using System.Text.Json.Serialization;
using Messaging.Platform.Api.Application.Messages;
using Messaging.Platform.Api.Infrastructure.Auth;
using Messaging.Platform.Api.Infrastructure.Http;
using Messaging.Platform.Persistence.Audit;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Messages;
using Messaging.Platform.Persistence.Participants;
using Messaging.Platform.Persistence.Reviews;
using Messaging.Platform.Persistence.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });

builder.Services.AddHealthChecks();

var connectionString = ResolveConnectionString(builder.Configuration);

builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddScoped<MessageReader>();
builder.Services.AddScoped<MessageWriter>();
builder.Services.AddScoped<ParticipantWriter>();
builder.Services.AddScoped<ReviewWriter>();
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<IMessageApplicationService, MessageApplicationService>();
builder.Services.AddScoped<UserContextAccessor>();
builder.Services.AddScoped<IUserResolver, UserResolver>();

ConfigureAuthentication(builder.Services, builder.Configuration, builder.Environment);
ConfigureAuthorization(builder.Services);

var app = builder.Build();

app.UseMiddleware<ApiExceptionMappingMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<UserResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static void ConfigureAuthentication(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
{
    const string defaultScheme = "MessagingAuth";

    var authentication = services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = defaultScheme;
        options.DefaultChallengeScheme = defaultScheme;
    });

    authentication.AddPolicyScheme(defaultScheme, defaultScheme, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (environment.IsDevelopment() && context.Request.Headers.ContainsKey("X-Debug-User"))
            {
                return DebugHeaderAuthenticationHandler.SchemeName;
            }

            return JwtBearerDefaults.AuthenticationScheme;
        };
    });

    authentication.AddJwtBearer(options =>
    {
        options.Authority = "https://accounts.google.com";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
            ValidateAudience = true,
            ValidAudience = configuration["Authentication:Google:ClientId"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                ?? string.Empty,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            NameClaimType = AuthClaimTypes.Email,
            RoleClaimType = AuthClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = GoogleJwtClaimNormalizer.NormalizeAsync
        };
    });

    if (environment.IsDevelopment())
    {
        authentication.AddScheme<AuthenticationSchemeOptions, DebugHeaderAuthenticationHandler>(
            DebugHeaderAuthenticationHandler.SchemeName,
            _ => { });
    }
}

static void ConfigureAuthorization(IServiceCollection services)
{
    services.AddSingleton<IAuthorizationHandler, PlatformRoleAuthorizationHandler>();

    services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.Viewer, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new PlatformRoleRequirement("viewer"));
        });

        options.AddPolicy(AuthorizationPolicies.Approver, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new PlatformRoleRequirement("approver"));
        });

        options.AddPolicy(AuthorizationPolicies.Admin, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new PlatformRoleRequirement("admin"));
        });
    });
}

static string ResolveConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("Messaging")
                           ?? configuration["Messaging:ConnectionString"]
                           ?? Environment.GetEnvironmentVariable("MESSAGING_DB_CONNECTION_STRING");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "Messaging database connection string is missing. Configure one of: ConnectionStrings:Messaging, Messaging:ConnectionString, or the MESSAGING_DB_CONNECTION_STRING environment variable.");

    return connectionString;
}

public partial class Program;
