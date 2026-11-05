namespace KRT.Onboarding.Domain.Common;

/// <summary>Violação de uma regra de negócio/invariante do domínio.</summary>
public class DomainException(string message) : Exception(message);
