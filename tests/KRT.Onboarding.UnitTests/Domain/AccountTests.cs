using KRT.Onboarding.Domain.Accounts;
using KRT.Onboarding.Domain.Common;

namespace KRT.Onboarding.UnitTests.Domain;

public class AccountTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Cpf ValidCpf = Cpf.Create("529.982.247-25");

    [Fact]
    public void Open_CreatesActiveAccount_AndRaisesCreatedEvent()
    {
        var account = Account.Open("Maria da Silva", ValidCpf, Now);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(Now, account.CreatedAt);

        var created = Assert.IsType<AccountCreatedEvent>(Assert.Single(account.DomainEvents));
        Assert.Equal(account.Id, created.AccountId);
        Assert.Equal("52998224725", created.Cpf);
        Assert.Equal(Now, created.OccurredAt);
    }

    [Fact]
    public void Open_NormalizesHolderNameSpaces()
    {
        var account = Account.Open("  Maria   da  Silva ", ValidCpf, Now);

        Assert.Equal("Maria da Silva", account.HolderName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Open_WithoutHolderName_ThrowsDomainException(string? holderName)
    {
        Assert.Throws<DomainException>(() => Account.Open(holderName!, ValidCpf, Now));
    }

    [Fact]
    public void Open_WithHolderNameTooLong_ThrowsDomainException()
    {
        var holderName = new string('A', Account.HolderNameMaxLength + 1);

        Assert.Throws<DomainException>(() => Account.Open(holderName, ValidCpf, Now));
    }

    [Fact]
    public void Update_ChangesData_AndRaisesUpdatedEvent()
    {
        var account = Account.Open("Maria da Silva", ValidCpf, Now);
        account.ClearDomainEvents();
        var later = Now.AddHours(1);

        account.Update("Maria Souza", AccountStatus.Inactive, later);

        Assert.Equal("Maria Souza", account.HolderName);
        Assert.Equal(AccountStatus.Inactive, account.Status);
        Assert.Equal(later, account.UpdatedAt);
        var updated = Assert.IsType<AccountUpdatedEvent>(Assert.Single(account.DomainEvents));
        Assert.Equal(AccountStatus.Inactive, updated.Status);
    }

    [Fact]
    public void Update_WithoutChanges_DoesNotRaiseEvent()
    {
        var account = Account.Open("Maria da Silva", ValidCpf, Now);
        account.ClearDomainEvents();

        account.Update("Maria da Silva", AccountStatus.Active, Now.AddHours(1));

        Assert.Empty(account.DomainEvents);
        Assert.Null(account.UpdatedAt);
    }

    [Fact]
    public void Update_WithInvalidStatus_ThrowsDomainException()
    {
        var account = Account.Open("Maria da Silva", ValidCpf, Now);

        Assert.Throws<DomainException>(() => account.Update("Maria da Silva", (AccountStatus)99, Now));
    }

    [Fact]
    public void Delete_RaisesDeletedEvent()
    {
        var account = Account.Open("Maria da Silva", ValidCpf, Now);
        account.ClearDomainEvents();

        account.Delete(Now);

        var deleted = Assert.IsType<AccountDeletedEvent>(Assert.Single(account.DomainEvents));
        Assert.Equal(account.Id, deleted.AccountId);
    }
}
