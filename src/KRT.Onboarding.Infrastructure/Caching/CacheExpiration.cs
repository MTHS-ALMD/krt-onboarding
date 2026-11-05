namespace KRT.Onboarding.Infrastructure.Caching;

public static class CacheExpiration
{
    /// <summary>
    /// Tempo restante até a meia-noite no fuso informado. A conta consultada
    /// fica em cache somente "naquele mesmo dia", como pede o requisito.
    /// </summary>
    public static TimeSpan UntilEndOfDay(DateTimeOffset utcNow, TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var nextMidnight = localNow.Date.AddDays(1);
        var nextMidnightOffset = new DateTimeOffset(nextMidnight, timeZone.GetUtcOffset(nextMidnight));

        return nextMidnightOffset - utcNow;
    }
}
