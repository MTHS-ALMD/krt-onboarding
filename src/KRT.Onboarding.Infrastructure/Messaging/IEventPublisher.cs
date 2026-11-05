using KRT.Onboarding.Infrastructure.Outbox;

namespace KRT.Onboarding.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
