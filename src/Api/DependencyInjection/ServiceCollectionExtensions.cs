using Messaging.Application.Messages;
using Messaging.Persistence.Audit;
using Messaging.Persistence.Db;
using Messaging.Persistence.Messages;
using Messaging.Persistence.Messages.Reads;
using Messaging.Persistence.Participants;
using Messaging.Persistence.Reviews;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(_ => new DbConnectionFactory(connectionString));
        services.AddScoped<MessageReader>();
        services.AddScoped<MessageWriter>();
        services.AddScoped<ParticipantWriter>();
        services.AddScoped<ReviewWriter>();
        services.AddScoped<AuditWriter>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageReadRepository, MessageReadRepository>();

        services.AddScoped<IMessageApplicationService, MessageApplicationService>();
        services.AddScoped<IMessageQueryService, MessageQueryService>();

        return services;
    }
}
