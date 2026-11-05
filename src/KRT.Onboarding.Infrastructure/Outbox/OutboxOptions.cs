namespace KRT.Onboarding.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 50;
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>Após N falhas a mensagem para de ser reprocessada (fica para análise/alerta).</summary>
    public int MaxAttempts { get; set; } = 10;
}
