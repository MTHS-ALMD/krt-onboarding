using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using KRT.Onboarding.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KRT.Onboarding.Infrastructure.Messaging;

/// <summary>
/// Publica os eventos de conta em um tópico SNS. As áreas consumidoras
/// assinam com filas SQS próprias e podem filtrar pelo atributo "eventType"
/// (ex.: cartões só quer AccountCreated) via subscription filter policy.
/// </summary>
internal sealed class SnsEventPublisher(
    IAmazonSimpleNotificationService sns,
    IOptions<MessagingOptions> options,
    ILogger<SnsEventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var topicArn = options.Value.TopicArn
            ?? throw new InvalidOperationException("Messaging:TopicArn não configurado.");

        var request = new PublishRequest
        {
            TopicArn = topicArn,
            Message = message.Payload,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["eventType"] = new() { DataType = "String", StringValue = message.Type },
                ["eventId"] = new() { DataType = "String", StringValue = message.Id.ToString() }
            }
        };

        // Tópico FIFO: garante a ordem dos eventos de uma mesma conta
        // (criada → atualizada → deletada) e deduplica pelo EventId.
        if (topicArn.EndsWith(".fifo", StringComparison.OrdinalIgnoreCase))
        {
            request.MessageGroupId = message.AggregateId.ToString();
            request.MessageDeduplicationId = message.Id.ToString();
        }

        var response = await sns.PublishAsync(request, cancellationToken);

        logger.LogInformation("Evento {EventType} ({EventId}) publicado no SNS. MessageId: {MessageId}",
            message.Type, message.Id, response.MessageId);
    }
}
