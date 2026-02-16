using System.Text.Json.Serialization;
using Messaging.Api.DependencyInjection;
using Messaging.Api.Infrastructure.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });

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
