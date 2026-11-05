using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KRT.Onboarding.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Aplica as migrations na subida (uso local/dev). Tenta algumas vezes
    /// porque o container do SQL Server demora alguns segundos para aceitar conexões.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, int maxAttempts = 10)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KrtDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<KrtDbContext>>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await context.Database.MigrateAsync();
                logger.LogInformation("Migrations aplicadas com sucesso");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Banco indisponível (tentativa {Attempt}/{Max}). Nova tentativa em 3s", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
