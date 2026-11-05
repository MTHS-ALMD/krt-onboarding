using KRT.Onboarding.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;

namespace KRT.Onboarding.Infrastructure.Messaging;

/// <summary>Publicador para desenvolvimento local sem AWS/LocalStack: apenas loga o evento.</summary>
internal sealed class LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) : IEventPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[Evento] {EventType} ({EventId}): {Payload}", message.Type, message.Id, message.Payload);
        return Task.CompletedTask;
    }
}
