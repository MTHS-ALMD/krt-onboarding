using KRT.Onboarding.Application.Accounts;
using KRT.Onboarding.Application.Common;
using KRT.Onboarding.Domain.Accounts;
using KRT.Onboarding.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace KRT.Onboarding.UnitTests.Application;

public class AccountServiceTests
{
    private const string ValidCpf = "529.982.247-25";

    private readonly Mock<IAccountRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAccountCache> _cache = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 5, 15, 0, 0, TimeSpan.Zero));
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _sut = new AccountService(_repository.Object, _unitOfWork.Object, _cache.Object, _time,
            NullLogger<AccountService>.Instance);
    }

    private static Account ExistingAccount() =>
        Account.Open("Maria da Silva", Cpf.Create(ValidCpf), new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task CreateAsync_WithValidData_PersistsAccountAndWarmsCache()
    {
        var response = await _sut.CreateAsync(new CreateAccountRequest("Maria da Silva", ValidCpf));

        Assert.Equal("52998224725", response.Cpf);
        Assert.Equal(AccountStatus.Active, response.Status);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, response.CreatedAt);
        _repository.Verify(r => r.AddAsync(It.Is<Account>(a => a.Id == response.Id), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SetAsync(response, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenCpfAlreadyExists_ThrowsConflictAndDoesNotPersist()
    {
        _repository.Setup(r => r.ExistsByCpfAsync(Cpf.Create(ValidCpf), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(new CreateAccountRequest("Maria da Silva", ValidCpf)));

        _repository.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidCpf_ThrowsDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.CreateAsync(new CreateAccountRequest("Maria da Silva", "123.456.789-00")));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenAccountIsCached_DoesNotQueryDatabase()
    {
        var cached = AccountResponse.FromDomain(ExistingAccount());
        _cache.Setup(c => c.GetAsync(cached.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cached);

        var response = await _sut.GetByIdAsync(cached.Id);

        Assert.Equal(cached, response);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenAccountIsNotCached_QueriesDatabaseAndCachesResult()
    {
        var account = ExistingAccount();
        _repository.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var response = await _sut.GetByIdAsync(account.Id);

        Assert.Equal(account.Id, response.Id);
        _repository.Verify(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SetAsync(response, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenAccountDoesNotExist_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByIdAsync(Guid.NewGuid()));

        _cache.Verify(c => c.SetAsync(It.IsAny<AccountResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAccountAndRefreshesCache()
    {
        var account = ExistingAccount();
        _repository.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var response = await _sut.UpdateAsync(account.Id, new UpdateAccountRequest("Maria Souza", AccountStatus.Inactive));

        Assert.Equal("Maria Souza", response.HolderName);
        Assert.Equal(AccountStatus.Inactive, response.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SetAsync(response, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenAccountDoesNotExist_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateAsync(Guid.NewGuid(), new UpdateAccountRequest("Maria", AccountStatus.Active)));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAccountAndEvictsCache()
    {
        var account = ExistingAccount();
        _repository.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        await _sut.DeleteAsync(account.Id);

        _repository.Verify(r => r.Remove(account), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveAsync(account.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(account.DomainEvents, e => e is AccountDeletedEvent);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(2, 500, 2, AccountService.MaxPageSize)]
    public async Task ListAsync_NormalizesPagination(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        _repository.Setup(r => r.ListAsync(expectedPage, expectedPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Account> { ExistingAccount() }, 1));

        var result = await _sut.ListAsync(page, pageSize);

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedPageSize, result.PageSize);
        Assert.Single(result.Items);
    }
}
