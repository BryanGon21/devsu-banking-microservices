using Devsu.Customers.Application.Contracts;
using Devsu.Customers.Application.Exceptions;
using Devsu.Customers.Application.Ports;
using Devsu.Customers.Application.Services;
using Devsu.Customers.Domain.Entities;
using Devsu.Customers.Domain.Enums;

namespace Devsu.Customers.UnitTests.Application;

public sealed class CustomerServiceTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_HashesPasswordAndDoesNotExposeItInResponse()
    {
        FakeCustomerRepository repository = new();
        FakeUnitOfWork unitOfWork = new();
        CustomerService service = CreateService(repository, unitOfWork);

        CustomerResponse response = await service.CreateAsync(CreateRequest(), CancellationToken.None);

        Customer customer = Assert.Single(repository.Customers);
        Assert.Equal("hash::1234", customer.PasswordHash);
        Assert.Equal("1002003004", customer.Identification);
        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.DoesNotContain(
            typeof(CustomerResponse).GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(customer.Id, response.CustomerId);
    }

    [Fact]
    public async Task Create_WithDuplicateIdentification_ThrowsConflictWithoutSaving()
    {
        FakeCustomerRepository repository = new();
        repository.Customers.Add(CreateEntity());
        FakeUnitOfWork unitOfWork = new();
        CustomerService service = CreateService(repository, unitOfWork);

        ConflictException exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("customer_duplicate_identification", exception.Code);
        Assert.Equal(0, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Update_MissingCustomer_ReturnsNotFoundBeforeCheckingDuplicate()
    {
        FakeCustomerRepository repository = new();
        repository.Customers.Add(CreateEntity());
        CustomerService service = CreateService(repository, new FakeUnitOfWork());
        Guid missingId = Guid.NewGuid();

        NotFoundException exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(
                missingId,
                new UpdateCustomerRequest(
                    "Another customer",
                    Gender.Other,
                    30,
                    "1002003004",
                    "Quito",
                    "099999999",
                    null,
                    true),
                CancellationToken.None));

        Assert.Equal("customer_not_found", exception.Code);
    }

    [Fact]
    public async Task Update_WithValidData_PreservesPasswordWhenOmitted()
    {
        Customer customer = CreateEntity();
        FakeCustomerRepository repository = new();
        repository.Customers.Add(customer);
        FakeUnitOfWork unitOfWork = new();
        CustomerService service = CreateService(repository, unitOfWork);

        CustomerResponse response = await service.UpdateAsync(
            customer.Id,
            new UpdateCustomerRequest(
                "Jose Lema Updated",
                Gender.Male,
                36,
                "1002003004",
                "Quito",
                "0980000000",
                null,
                false),
            CancellationToken.None);

        Assert.Equal("Jose Lema Updated", response.Name);
        Assert.Equal(36, response.Age);
        Assert.False(response.IsActive);
        Assert.Equal("hash::1234", customer.PasswordHash);
        Assert.Equal(2, response.Version);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Delete_RepeatedRequest_IsIdempotentAndSavesOnce()
    {
        Customer customer = CreateEntity();
        FakeCustomerRepository repository = new();
        repository.Customers.Add(customer);
        FakeUnitOfWork unitOfWork = new();
        CustomerService service = CreateService(repository, unitOfWork);

        await service.DeleteAsync(customer.Id, CancellationToken.None);
        await service.DeleteAsync(customer.Id, CancellationToken.None);

        Assert.True(customer.IsDeleted);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task List_WithPageThatOverflowsOffset_ThrowsValidation()
    {
        CustomerService service = CreateService(new FakeCustomerRepository(), new FakeUnitOfWork());

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ListAsync(int.MaxValue, 100, null, CancellationToken.None));

        Assert.Equal("page_out_of_range", exception.Code);
    }

    private static CustomerService CreateService(
        FakeCustomerRepository repository,
        FakeUnitOfWork unitOfWork)
    {
        return new CustomerService(
            repository,
            new FakePasswordHasher(),
            unitOfWork,
            new FakeTimeProvider(CurrentTime));
    }

    private static CreateCustomerRequest CreateRequest()
    {
        return new CreateCustomerRequest(
            "Jose Lema",
            Gender.Male,
            35,
            " 1002003004 ",
            "123 Main Street",
            "098254785",
            "1234",
            true);
    }

    private static Customer CreateEntity()
    {
        return Customer.Create(
            Guid.NewGuid(),
            "Jose Lema",
            Gender.Male,
            35,
            "1002003004",
            "123 Main Street",
            "098254785",
            "hash::1234",
            true,
            CurrentTime);
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = [];

        public Task<Customer?> GetByIdAsync(
            Guid customerId,
            bool includeDeleted,
            CancellationToken cancellationToken)
        {
            Customer? customer = Customers.SingleOrDefault(item =>
                item.Id == customerId && (includeDeleted || !item.IsDeleted));

            return Task.FromResult(customer);
        }

        public Task<(IReadOnlyCollection<Customer> Items, int Total)> ListAsync(
            int skip,
            int take,
            bool? isActive,
            CancellationToken cancellationToken)
        {
            Customer[] filteredCustomers = Customers
                .Where(customer =>
                    !customer.IsDeleted && (!isActive.HasValue || customer.IsActive == isActive.Value))
                .OrderBy(customer => customer.Name)
                .Skip(skip)
                .Take(take)
                .ToArray();

            int total = Customers.Count(customer =>
                !customer.IsDeleted && (!isActive.HasValue || customer.IsActive == isActive.Value));

            return Task.FromResult(((IReadOnlyCollection<Customer>)filteredCustomers, total));
        }

        public Task<bool> IdentificationExistsAsync(
            string identification,
            Guid? excludedCustomerId,
            CancellationToken cancellationToken)
        {
            bool exists = Customers.Any(customer =>
                string.Equals(customer.Identification, identification, StringComparison.OrdinalIgnoreCase) &&
                (!excludedCustomerId.HasValue || customer.Id != excludedCustomerId.Value));

            return Task.FromResult(exists);
        }

        public void Add(Customer customer)
        {
            Customers.Add(customer);
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

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
            return $"hash::{password}";
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
