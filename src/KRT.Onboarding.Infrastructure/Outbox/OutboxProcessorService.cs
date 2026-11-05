using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KRT.Onboarding.Infrastructure.Outbox;

/// <summary>Worker em background que publica periodicamente as mensagens do Outbox.</summary>
internal sealed class OutboxProcessorService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds));

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Nunca deixa o worker morrer: tenta de novo no próximo ciclo.
                logger.LogError(ex, "Erro ao processar o Outbox");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
