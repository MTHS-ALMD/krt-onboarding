namespace KRT.Onboarding.Application.Accounts;

/// <summary>
/// Cache de leitura das contas. A implementação deve ser resiliente:
/// uma falha no cache nunca pode derrubar a requisição (cai para o banco).
/// </summary>
public interface IAccountCache
{
    Task<AccountResponse?> GetAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task SetAsync(AccountResponse account, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid accountId, CancellationToken cancellationToken = default);
}
