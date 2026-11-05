namespace KRT.Onboarding.Infrastructure.Caching;

public sealed class AccountCacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Fuso usado para definir o "fim do dia" em que o cache expira.</summary>
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
}
