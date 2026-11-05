namespace KRT.Onboarding.Domain.Accounts;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCpfAsync(Cpf cpf, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Account> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    void Remove(Account account);
}
