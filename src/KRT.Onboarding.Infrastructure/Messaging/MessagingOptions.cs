namespace KRT.Onboarding.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>
    /// ARN do tópico SNS de eventos de conta. Cada área (fraude, cartões...)
    /// assina o tópico com a própria fila SQS (fan-out). Vazio = apenas loga os eventos.
    /// </summary>
    public string? TopicArn { get; set; }
}
