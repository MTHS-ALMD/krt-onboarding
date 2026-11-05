using System.Text.Json;
using KRT.Onboarding.Domain.Common;
using KRT.Onboarding.Infrastructure.Serialization;

namespace KRT.Onboarding.Infrastructure.Outbox;

/// <summary>
/// Evento de domínio persistido na mesma transação da alteração da conta
/// (Transactional Outbox). Garante que nenhuma área deixe de ser avisada
/// caso o SNS esteja indisponível no momento da escrita.
/// </summary>
public sealed class OutboxMessage
{
    public const int TypeMaxLength = 100;
    public const int ErrorMaxLength = 2000;

    public Guid Id { get; private set; }
    public Guid AggregateId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage FromDomainEvent(IDomainEvent domainEvent, Guid aggregateId)
    {
        var eventType = domainEvent.GetType();
        var name = eventType.Name.EndsWith("Event", StringComparison.Ordinal) ? eventType.Name[..^"Event".Length] : eventType.Name;

        return new OutboxMessage
        {
            Id = domainEvent.EventId, // EventId vira a chave de idempotência dos consumidores.
            AggregateId = aggregateId,
            Type = name,
            Payload = JsonSerializer.Serialize(domainEvent, eventType, JsonDefaults.Options),
            OccurredAt = domainEvent.OccurredAt
        };
    }

    public void MarkAsProcessed(DateTime processedAt)
    {
        ProcessedAt = processedAt;
        Attempts++;
        LastError = null;
    }

    public void RegisterFailure(string error)
    {
        Attempts++;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;
    }
}
