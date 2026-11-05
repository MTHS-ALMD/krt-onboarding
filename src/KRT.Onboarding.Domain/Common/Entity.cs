namespace KRT.Onboarding.Domain.Common;

/// <summary>
/// Base para entidades/agregados. Acumula eventos de domínio que serão
/// persistidos no Outbox na mesma transação da alteração.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
