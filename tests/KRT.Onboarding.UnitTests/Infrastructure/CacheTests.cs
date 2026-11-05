using KRT.Onboarding.Application.Accounts;
using KRT.Onboarding.Domain.Accounts;
using KRT.Onboarding.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace KRT.Onboarding.UnitTests.Infrastructure;

public class CacheTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static readonly AccountResponse Account = new(
        Guid.NewGuid(), "Maria da Silva", "52998224725", AccountStatus.Active,
        new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc), null);

    private static DistributedAccountCache CreateCache(IDistributedCache distributedCache, TimeProvider time) =>
        new(distributedCache, time, Options.Create(new AccountCacheOptions()), NullLogger<DistributedAccountCache>.Instance);

    [Theory]
    [InlineData("2026-11-05T15:00:00Z", 12 * 60)]  // 12:00 em SP → 12h até a meia-noite
    [InlineData("2026-11-06T02:30:00Z", 30)]       // 23:30 em SP → 30 min
    [InlineData("2026-11-06T03:00:00Z", 24 * 60)]  // 00:00 em SP → dia inteiro
    public void UntilEndOfDay_ReturnsTimeUntilLocalMidnight(string utcNow, int expectedMinutes)
    {
        var ttl = CacheExpiration.UntilEndOfDay(DateTimeOffset.Parse(utcNow), SaoPaulo);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), ttl);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsSameAccount()
    {
        var memoryCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = CreateCache(memoryCache, TimeProvider.System);

        await cache.SetAsync(Account);
        var cached = await cache.GetAsync(Account.Id);

        Assert.Equal(Account, cached);
    }

    [Fact]
    public async Task RemoveAsync_EvictsAccount()
    {
        var memoryCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = CreateCache(memoryCache, TimeProvider.System);
        await cache.SetAsync(Account);

        await cache.RemoveAsync(Account.Id);

        Assert.Null(await cache.GetAsync(Account.Id));
    }

    [Fact]
    public async Task SetAsync_ExpiresAtEndOfDay()
    {
        var distributedCache = new Mock<IDistributedCache>();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-05T15:00:00Z"));
        var cache = CreateCache(distributedCache.Object, time);

        await cache.SetAsync(Account);

        distributedCache.Verify(c => c.SetAsync(
            DistributedAccountCache.BuildKey(Account.Id),
            It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromHours(12)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenCacheIsUnavailable_ReturnsNullInsteadOfThrowing()
    {
        var distributedCache = new Mock<IDistributedCache>();
        distributedCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis fora do ar"));
        var cache = CreateCache(distributedCache.Object, TimeProvider.System);

        Assert.Null(await cache.GetAsync(Account.Id));
    }
}
