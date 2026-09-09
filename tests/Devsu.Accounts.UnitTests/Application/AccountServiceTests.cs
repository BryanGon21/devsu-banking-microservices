using Devsu.Accounts.Application.Contracts;
using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Application.Services;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.UnitTests.Application;

public sealed class AccountServiceTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_WithActiveProjection_PersistsAccount()
    {
        Guid customerId = Guid.NewGuid();
        FakeAccountRepository repository = new();
        FakeUnitOfWork unitOfWork = new();
        AccountService service = CreateService(
            repository,
            new FakeCustomerProjectionReader(CreateProjection(customerId, isActive: true)),
            unitOfWork);

        AccountResponse response = await service.CreateAsync(
            CreateRequest(customerId),
            CancellationToken.None);

        Account account = Assert.Single(repository.Accounts);
        Assert.Equal("001234", account.Number);
        Assert.Equal(100m, account.CurrentBalance);
        Assert.Equal(customerId, account.CustomerId);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal(account.Number, response.Number);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task Create_WhenProjectionIsUnavailable_ReturnsRetryableConflict(
        bool projectionExists,
        bool isActive)
    {
        Guid customerId = Guid.NewGuid();
        CustomerProjection? projection = projectionExists
            ? CreateProjection(customerId, isActive)
            : null;
        FakeAccountRepository repository = new();
        FakeUnitOfWork unitOfWork = new();
        AccountService service = CreateService(
            repository,
            new FakeCustomerProjectionReader(projection),
            unitOfWork);

        ConflictException exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(CreateRequest(customerId), CancellationToken.None));

        Assert.Equal("cliente_no_disponible", exception.Code);
        Assert.Contains("Reintente", exception.Message, StringComparison.Ordinal);
        Assert.Empty(repository.Accounts);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Create_WithDuplicateNumber_DoesNotReadProjectionOrSave()
    {
        Guid customerId = Guid.NewGuid();
        FakeAccountRepository repository = new();
        repository.Accounts.Add(CreateAccount("001234", customerId));
        FakeCustomerProjectionReader projectionReader = new(CreateProjection(customerId, true));
        FakeUnitOfWork unitOfWork = new();
        AccountService service = CreateService(repository, projectionReader, unitOfWork);

        ConflictException exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(CreateRequest(customerId), CancellationToken.None));

        Assert.Equal("account_duplicate_number", exception.Code);
        Assert.Equal(0, projectionReader.ReadCalls);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task ChangeStatus_RepeatedRequest_SavesOnce()
    {
        Guid customerId = Guid.NewGuid();
        FakeAccountRepository repository = new();
        repository.Accounts.Add(CreateAccount("001234", customerId));
        FakeUnitOfWork unitOfWork = new();
        AccountService service = CreateService(
            repository,
            new FakeCustomerProjectionReader(CreateProjection(customerId, true)),
            unitOfWork);

        await service.ChangeStatusAsync(
            "001234",
            new ChangeAccountStatusRequest(false),
            CancellationToken.None);
        await service.ChangeStatusAsync(
            "001234",
            new ChangeAccountStatusRequest(false),
            CancellationToken.None);

        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task List_WithOverflowingPage_ThrowsValidation()
    {
        AccountService service = CreateService(
            new FakeAccountRepository(),
            new FakeCustomerProjectionReader(null),
            new FakeUnitOfWork());

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ListAsync(int.MaxValue, 100, null, null, CancellationToken.None));

        Assert.Equal("page_out_of_range", exception.Code);
    }

    private static AccountService CreateService(
        FakeAccountRepository repository,
        FakeCustomerProjectionReader projectionReader,
        FakeUnitOfWork unitOfWork)
    {
        return new AccountService(
            repository,
            projectionReader,
            unitOfWork,
            new FixedTimeProvider(CurrentTime));
    }

    private static CreateAccountRequest CreateRequest(Guid customerId)
    {
        return new CreateAccountRequest(
            "001234",
            AccountType.Savings,
            100m,
            true,
            customerId);
    }

    private static Account CreateAccount(string number, Guid customerId)
    {
        return Account.Create(
            number,
            AccountType.Savings,
            100m,
            true,
            customerId,
            CurrentTime);
    }

    private static CustomerProjection CreateProjection(Guid customerId, bool isActive)
    {
        return CustomerProjection.Create(
            customerId,
            "Jose Lema",
            isActive,
            false,
            1,
            CurrentTime);
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public List<Account> Accounts { get; } = [];

        public Task AddAsync(Account account, CancellationToken cancellationToken)
        {
            Accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string number, CancellationToken cancellationToken)
        {
            return Task.FromResult(Accounts.Any(account => account.Number == number));
        }

        public Task<Account?> GetByNumberAsync(string number, CancellationToken cancellationToken)
        {
            return Task.FromResult(Accounts.SingleOrDefault(account => account.Number == number));
        }

        public Task<(IReadOnlyCollection<Account> Items, int TotalItems)> ListAsync(
            int skip,
            int take,
            Guid? customerId,
            bool? isActive,
            CancellationToken cancellationToken)
        {
            IEnumerable<Account> query = Accounts;
            if (customerId.HasValue)
            {
                query = query.Where(account => account.CustomerId == customerId.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(account => account.IsActive == isActive.Value);
            }

            Account[] matches = query.ToArray();
            IReadOnlyCollection<Account> items = matches.Skip(skip).Take(take).ToArray();
            return Task.FromResult((items, matches.Length));
        }
    }

    private sealed class FakeCustomerProjectionReader : ICustomerProjectionReader
    {
        private readonly CustomerProjection? _projection;

        public FakeCustomerProjectionReader(CustomerProjection? projection)
        {
            _projection = projection;
        }

        public int ReadCalls { get; private set; }

        public Task<CustomerProjection?> GetByIdAsync(
            Guid customerId,
            CancellationToken cancellationToken)
        {
            ReadCalls++;
            CustomerProjection? result = _projection?.CustomerId == customerId
                ? _projection
                : null;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
