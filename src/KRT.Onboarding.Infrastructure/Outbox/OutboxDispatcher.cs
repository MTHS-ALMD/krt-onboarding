using KRT.Onboarding.Infrastructure.Messaging;
using KRT.Onboarding.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KRT.Onboarding.Infrastructure.Outbox;

/// <summary>
/// Lê as mensagens pendentes do Outbox e publica no broker.
/// Entrega "at-least-once": consumidores devem ser idempotentes pelo eventId.
/// </summary>
public sealed class OutboxDispatcher(
    KrtDbContext context,
    IEventPublisher publisher,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger)
{
    /// <returns>Quantidade de mensagens publicadas com sucesso.</returns>
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        var pending = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < settings.MaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        var published = 0;

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.MarkAsProcessed(timeProvider.GetUtcNow().UtcDateTime);
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RegisterFailure(ex.Message);
                logger.LogError(ex, "Falha ao publicar evento {EventType} ({EventId}). Tentativa {Attempt}",
                    message.Type, message.Id, message.Attempts);

                // Interrompe o lote para preservar a ordem dos eventos:
                // não publica "Deleted" antes de um "Created" que falhou.
                break;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return published;
    }
}
