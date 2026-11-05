using System.Text.Json;
using KRT.Onboarding.Application.Accounts;
using KRT.Onboarding.Infrastructure.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KRT.Onboarding.Infrastructure.Caching;

/// <summary>
/// Cache de contas sobre IDistributedCache (Redis/ElastiCache em produção).
/// Distribuído para ser compartilhado por todas as instâncias da API.
/// Falhas no cache são logadas e ignoradas: a API continua respondendo pelo banco.
/// </summary>
public sealed class DistributedAccountCache(
    IDistributedCache cache,
    TimeProvider timeProvider,
    IOptions<AccountCacheOptions> options,
    ILogger<DistributedAccountCache> logger) : IAccountCache
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);

    public static string BuildKey(Guid accountId) => $"account:{accountId}";

    public async Task<AccountResponse?> GetAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await cache.GetAsync(BuildKey(accountId), cancellationToken);
            return bytes is null ? null : JsonSerializer.Deserialize<AccountResponse>(bytes, JsonDefaults.Options);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao ler a conta {AccountId} do cache", accountId);
            return null;
        }
    }

    public async Task SetAsync(AccountResponse account, CancellationToken cancellationToken = default)
    {
        try
        {
            var entryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration.UntilEndOfDay(timeProvider.GetUtcNow(), _timeZone)
            };

            var bytes = JsonSerializer.SerializeToUtf8Bytes(account, JsonDefaults.Options);
            await cache.SetAsync(BuildKey(account.Id), bytes, entryOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao gravar a conta {AccountId} no cache", account.Id);
        }
    }

    public async Task RemoveAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(BuildKey(accountId), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao remover a conta {AccountId} do cache", accountId);
        }
    }
}
