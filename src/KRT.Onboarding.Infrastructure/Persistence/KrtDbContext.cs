using KRT.Onboarding.Application.Common;
using KRT.Onboarding.Domain.Accounts;
using KRT.Onboarding.Domain.Common;
using KRT.Onboarding.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KRT.Onboarding.Infrastructure.Persistence;

public sealed class KrtDbContext(DbContextOptions<KrtDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KrtDbContext).Assembly);

    // Todas as datas são gravadas em UTC; o SQL Server não guarda o "Kind",
    // então marcamos como UTC na leitura para a API serializar com "Z".
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Os eventos de domínio são gravados como mensagens do Outbox na
        // MESMA transação da alteração: ou salva tudo, ou não salva nada.
        AddDomainEventsToOutbox();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("A conta foi alterada por outra operação. Recarregue os dados e tente novamente.");
        }
        catch (DbUpdateException ex) when (IsUniqueKeyViolation(ex))
        {
            throw new ConflictException("Já existe uma conta cadastrada para este CPF.");
        }
    }

    private void AddDomainEventsToOutbox()
    {
        var entities = ChangeTracker.Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
                OutboxMessages.Add(OutboxMessage.FromDomainEvent(domainEvent, entity.Id));

            entity.ClearDomainEvents();
        }
    }

    // 2601/2627 = violação de índice único/PK no SQL Server
    // (ex.: duas requisições simultâneas com o mesmo CPF).
    private static bool IsUniqueKeyViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
