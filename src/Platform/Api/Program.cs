using System.Text.Json.Serialization;
using Messaging.Platform.Api.Application.Messages;
using Messaging.Platform.Api.Infrastructure.Http;
using Messaging.Platform.Persistence.Audit;
using Messaging.Platform.Persistence.Db;
using Messaging.Platform.Persistence.Messages;
using Messaging.Platform.Persistence.Participants;
using Messaging.Platform.Persistence.Reviews;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddHealthChecks();

var connectionString = ResolveConnectionString(builder.Configuration);

builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddScoped<MessageReader>();
builder.Services.AddScoped<MessageWriter>();
builder.Services.AddScoped<ParticipantWriter>();
builder.Services.AddScoped<ReviewWriter>();
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<IMessageApplicationService, MessageApplicationService>();

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
    {
        throw new InvalidOperationException(
            "Messaging database connection string is missing. Configure one of: ConnectionStrings:Messaging, Messaging:ConnectionString, or the MESSAGING_DB_CONNECTION_STRING environment variable.");
    }

    return connectionString;
}

public partial class Program;
