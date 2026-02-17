using System.Text.Json.Serialization;
using Messaging.Api.DependencyInjection;
using Messaging.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    static string NormalizeModelKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var bracketIndex = segment.IndexOf('[');
            if (bracketIndex >= 0)
            {
                var head = segment[..bracketIndex];
                var tail = segment[bracketIndex..];
                if (head.Length > 0)
                    segments[i] = char.ToLowerInvariant(head[0]) + head[1..] + tail;
            }
            else
            {
                segments[i] = char.ToLowerInvariant(segment[0]) + segment[1..];
            }
        }
        return string.Join('.', segments);
    }

    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => NormalizeModelKey(kvp.Key),
                kvp => kvp.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Invalid value."
                        : error.ErrorMessage)
                    .ToArray());

        return new ObjectResult(
            new ApiErrorResponse("INVALID_REQUEST", "Validation failed.", errors))
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
    };
});

builder.Services.AddHealthChecks();

var connectionString = ResolveConnectionString(builder.Configuration);

builder.Services.AddMessaging(connectionString);

var app = builder.Build();

app.UseMiddleware<ApiExceptionMappingMiddleware>();
app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

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
