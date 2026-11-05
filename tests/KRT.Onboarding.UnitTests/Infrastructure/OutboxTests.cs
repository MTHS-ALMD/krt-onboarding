using KRT.Onboarding.Domain.Accounts;
using KRT.Onboarding.Infrastructure.Messaging;
using KRT.Onboarding.Infrastructure.Outbox;
using KRT.Onboarding.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace KRT.Onboarding.UnitTests.Infrastructure;

public class OutboxTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc);

    private readonly KrtDbContext _context = new(
        new DbContextOptionsBuilder<KrtDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly Mock<IEventPublisher> _publisher = new();

    private OutboxDispatcher CreateDispatcher() => new(
        _context, _publisher.Object, new FakeTimeProvider(new DateTimeOffset(Now)),
        Options.Create(new OutboxOptions()), NullLogger<OutboxDispatcher>.Instance);

    private async Task<Account> CreateAccountAsync(string cpf = "529.982.247-25")
    {
        var account = Account.Open("Maria da Silva", Cpf.Create(cpf), Now);
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        return account;
    }

    [Fact]
    public async Task SaveChanges_StoresDomainEventsInOutbox_InSameUnitOfWork()
    {
        var account = await CreateAccountAsync();

        var message = Assert.Single(await _context.OutboxMessages.ToListAsync());
        Assert.Equal("AccountCreated", message.Type);
        Assert.Equal(account.Id, message.AggregateId);
        Assert.Contains(account.Id.ToString(), message.Payload);
        Assert.Contains("\"status\":\"Active\"", message.Payload);
        Assert.Null(message.ProcessedAt);
        Assert.Empty(account.DomainEvents);
    }

    [Fact]
    public async Task DispatchPendingAsync_PublishesAndMarksMessagesAsProcessed()
    {
        await CreateAccountAsync();

        var published = await CreateDispatcher().DispatchPendingAsync();

        Assert.Equal(1, published);
        _publisher.Verify(p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Type == "AccountCreated"), It.IsAny<CancellationToken>()), Times.Once);
        var message = Assert.Single(await _context.OutboxMessages.ToListAsync());
        Assert.Equal(Now, message.ProcessedAt);

        // Mensagens já processadas não são publicadas de novo.
        Assert.Equal(0, await CreateDispatcher().DispatchPendingAsync());
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenPublishFails_StopsBatchToKeepOrder()
    {
        var account = await CreateAccountAsync();
        account.Update("Maria Souza", AccountStatus.Inactive, Now.AddMinutes(1));
        await _context.SaveChangesAsync();

        _publisher.Setup(p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Type == "AccountCreated"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SNS indisponível"));

        var published = await CreateDispatcher().DispatchPendingAsync();

        Assert.Equal(0, published);
        _publisher.Verify(p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Type == "AccountUpdated"), It.IsAny<CancellationToken>()), Times.Never);

        var failed = await _context.OutboxMessages.SingleAsync(m => m.Type == "AccountCreated");
        Assert.Equal(1, failed.Attempts);
        Assert.Null(failed.ProcessedAt);
        Assert.Equal("SNS indisponível", failed.LastError);
    }
}
