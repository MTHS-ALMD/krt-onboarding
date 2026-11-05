using KRT.Onboarding.Domain.Common;

namespace KRT.Onboarding.Domain.Accounts;

/// <summary>
/// Agregado raiz "Conta". Toda mudança de estado passa por métodos do
/// próprio agregado, que validam as invariantes e registram o evento de domínio.
/// </summary>
public sealed class Account : Entity
{
    public const int HolderNameMaxLength = 150;

    public string HolderName { get; private set; } = null!;
    public Cpf Cpf { get; private set; } = null!;
    public AccountStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Construtor exigido pelo EF Core.
    private Account() { }

    public static Account Open(string holderName, Cpf cpf, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            HolderName = NormalizeHolderName(holderName),
            Cpf = cpf,
            Status = AccountStatus.Active,
            CreatedAt = now
        };

        account.RaiseDomainEvent(new AccountCreatedEvent(account.Id, account.HolderName, cpf.Value, account.Status)
        {
            OccurredAt = now
        });

        return account;
    }

    /// <summary>
    /// Atualiza nome do titular e status. O CPF é imutável: identifica o titular.
    /// </summary>
    public void Update(string holderName, AccountStatus status, DateTime now)
    {
        var normalizedName = NormalizeHolderName(holderName);

        if (!Enum.IsDefined(status))
            throw new DomainException("O status da conta é inválido.");

        if (normalizedName == HolderName && status == Status)
            return; // Nada mudou: não gera evento desnecessário para as outras áreas.

        HolderName = normalizedName;
        Status = status;
        UpdatedAt = now;

        RaiseDomainEvent(new AccountUpdatedEvent(Id, HolderName, Cpf.Value, Status) { OccurredAt = now });
    }

    public void Delete(DateTime now) =>
        RaiseDomainEvent(new AccountDeletedEvent(Id, Cpf.Value) { OccurredAt = now });

    private static string NormalizeHolderName(string? holderName)
    {
        if (string.IsNullOrWhiteSpace(holderName))
            throw new DomainException("O nome do titular é obrigatório.");

        var normalized = string.Join(' ', holderName.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length > HolderNameMaxLength)
            throw new DomainException($"O nome do titular deve ter no máximo {HolderNameMaxLength} caracteres.");

        return normalized;
    }
}
