using KRT.Onboarding.Application.Common;

namespace KRT.Onboarding.Application.Accounts;

public interface IAccountService
{
    Task<AccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);

    Task<AccountResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<AccountResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AccountResponse> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
