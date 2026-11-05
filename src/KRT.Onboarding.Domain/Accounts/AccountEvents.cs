using KRT.Onboarding.Domain.Common;

namespace KRT.Onboarding.Domain.Accounts;

// Os eventos carregam o estado da conta ("event-carried state transfer"):
// as áreas consumidoras (fraude, cartões...) podem manter sua própria cópia
// dos dados e não precisam consultar esta API a cada transação.

public sealed record AccountCreatedEvent(
    Guid AccountId,
    string HolderName,
    string Cpf,
    AccountStatus Status) : DomainEvent;

public sealed record AccountUpdatedEvent(
    Guid AccountId,
    string HolderName,
    string Cpf,
    AccountStatus Status) : DomainEvent;

public sealed record AccountDeletedEvent(
    Guid AccountId,
    string Cpf) : DomainEvent;
