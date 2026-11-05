using KRT.Onboarding.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace KRT.Onboarding.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(KrtDbContext context) : IAccountRepository
{
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> ExistsByCpfAsync(Cpf cpf, CancellationToken cancellationToken = default) =>
        context.Accounts.AnyAsync(a => a.Cpf == cpf, cancellationToken);

    public async Task<(IReadOnlyList<Account> Items, int TotalCount)> ListAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = context.Accounts.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default) =>
        await context.Accounts.AddAsync(account, cancellationToken);

    public void Remove(Account account) => context.Accounts.Remove(account);
}
