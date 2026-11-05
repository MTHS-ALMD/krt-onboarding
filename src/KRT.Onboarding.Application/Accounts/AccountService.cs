using KRT.Onboarding.Application.Common;
using KRT.Onboarding.Domain.Accounts;
using KRT.Onboarding.Domain.Common;
using Microsoft.Extensions.Logging;

namespace KRT.Onboarding.Application.Accounts;

/// <summary>
/// Casos de uso de conta. Leituras por Id usam cache-aside
/// (cache → banco → cache) e escritas mantêm o cache coerente.
/// A publicação dos eventos para as outras áreas é feita pelo Outbox
/// (ver Infrastructure), na mesma transação da escrita.
/// </summary>
public sealed class AccountService(
    IAccountRepository repository,
    IUnitOfWork unitOfWork,
    IAccountCache cache,
    TimeProvider timeProvider,
    ILogger<AccountService> logger) : IAccountService
{
    public const int MaxPageSize = 100;

    public async Task<AccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = Cpf.Create(request.Cpf);

        if (await repository.ExistsByCpfAsync(cpf, cancellationToken))
            throw new ConflictException("Já existe uma conta cadastrada para este CPF.");

        var account = Account.Open(request.HolderName, cpf, Now());

        await repository.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Conta {AccountId} criada", account.Id);

        // Aquece o cache: as áreas que recebem o evento de criação
        // tendem a consultar a conta logo em seguida.
        var response = AccountResponse.FromDomain(account);
        await cache.SetAsync(response, cancellationToken);

        return response;
    }

    public async Task<AccountResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync(id, cancellationToken);
        if (cached is not null)
            return cached;

        var account = await GetAccountOrThrowAsync(id, cancellationToken);

        var response = AccountResponse.FromDomain(account);
        await cache.SetAsync(response, cancellationToken);

        return response;
    }

    public async Task<PagedResult<AccountResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, totalCount) = await repository.ListAsync(page, pageSize, cancellationToken);

        return new PagedResult<AccountResponse>(
            items.Select(AccountResponse.FromDomain).ToList(), page, pageSize, totalCount);
    }

    public async Task<AccountResponse> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await GetAccountOrThrowAsync(id, cancellationToken);

        account.Update(request.HolderName, request.Status, Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Conta {AccountId} atualizada", account.Id);

        // Write-through: o cache passa a refletir a versão nova imediatamente.
        var response = AccountResponse.FromDomain(account);
        await cache.SetAsync(response, cancellationToken);

        return response;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await GetAccountOrThrowAsync(id, cancellationToken);

        account.Delete(Now());
        repository.Remove(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Conta {AccountId} removida", account.Id);

        await cache.RemoveAsync(id, cancellationToken);
    }

    private async Task<Account> GetAccountOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Conta {id} não encontrada.");

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
