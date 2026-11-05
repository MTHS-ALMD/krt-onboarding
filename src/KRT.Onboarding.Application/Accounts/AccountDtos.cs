using System.ComponentModel.DataAnnotations;
using KRT.Onboarding.Domain.Accounts;

namespace KRT.Onboarding.Application.Accounts;

public sealed record CreateAccountRequest(
    [Required] string HolderName,
    [Required] string Cpf);

public sealed record UpdateAccountRequest(
    [Required] string HolderName,
    [Required] AccountStatus Status);

public sealed record AccountResponse(
    Guid Id,
    string HolderName,
    string Cpf,
    AccountStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static AccountResponse FromDomain(Account account) => new(
        account.Id,
        account.HolderName,
        account.Cpf.Value,
        account.Status,
        account.CreatedAt,
        account.UpdatedAt);
}
