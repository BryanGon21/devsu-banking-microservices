using Devsu.Customers.Application.Contracts;
using Devsu.Customers.Application.Exceptions;
using Devsu.Customers.Application.Ports;
using Devsu.Customers.Domain.Entities;

namespace Devsu.Customers.Application.Services;

public sealed class CustomerService : ICustomerService
{
    private const int MinimumPasswordLength = 4;
    private const int MaximumPasswordLength = 100;
    private const int MaximumPageSize = 100;

    private readonly ICustomerRepository _customerRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CustomerService(
        ICustomerRepository customerRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _customerRepository = customerRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePassword(request.Password);

        string identification = NormalizeIdentification(request.Identification);
        if (await _customerRepository.IdentificationExistsAsync(identification, null, cancellationToken))
        {
            throw new ConflictException(
                "customer_duplicate_identification",
                "A customer with the specified identification already exists.");
        }

        string passwordHash = _passwordHasher.Hash(request.Password);
        Customer customer = Customer.Create(
            Guid.NewGuid(),
            request.Name,
            request.Gender,
            request.Age,
            identification,
            request.Address,
            request.Phone,
            passwordHash,
            request.IsActive,
            _timeProvider.GetUtcNow());

        _customerRepository.Add(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    public async Task<CustomerResponse> GetByIdAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);
        Customer customer = await GetExistingCustomerAsync(customerId, cancellationToken);
        return Map(customer);
    }

    public async Task<PageResponse<CustomerResponse>> ListAsync(
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new ValidationException("invalid_page", "Page must be greater than or equal to 1.");
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ValidationException(
                "invalid_page_size",
                $"Page size must be between 1 and {MaximumPageSize}.");
        }

        long calculatedSkip = ((long)page - 1) * pageSize;
        if (calculatedSkip > int.MaxValue)
        {
            throw new ValidationException("page_out_of_range", "The requested page is out of range.");
        }

        int skip = (int)calculatedSkip;
        (IReadOnlyCollection<Customer> customers, int total) = await _customerRepository.ListAsync(
            skip,
            pageSize,
            isActive,
            cancellationToken);

        CustomerResponse[] items = customers.Select(Map).ToArray();
        int totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        return new PageResponse<CustomerResponse>(items, page, pageSize, total, totalPages);
    }

    public async Task<CustomerResponse> UpdateAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);
        ArgumentNullException.ThrowIfNull(request);

        Customer customer = await GetExistingCustomerAsync(customerId, cancellationToken);

        string identification = NormalizeIdentification(request.Identification);
        if (await _customerRepository.IdentificationExistsAsync(
                identification,
                customerId,
                cancellationToken))
        {
            throw new ConflictException(
                "customer_duplicate_identification",
                "Another customer with the specified identification already exists.");
        }

        string? newPasswordHash = null;
        if (request.Password is not null)
        {
            ValidatePassword(request.Password);
            newPasswordHash = _passwordHasher.Hash(request.Password);
        }

        bool changed = customer.Update(
            request.Name,
            request.Gender,
            request.Age,
            identification,
            request.Address,
            request.Phone,
            newPasswordHash,
            request.IsActive,
            _timeProvider.GetUtcNow());

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Map(customer);
    }

    public async Task<CustomerResponse> ChangeStatusAsync(
        Guid customerId,
        ChangeCustomerStatusRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);
        ArgumentNullException.ThrowIfNull(request);

        Customer customer = await GetExistingCustomerAsync(customerId, cancellationToken);
        if (customer.ChangeStatus(request.IsActive, _timeProvider.GetUtcNow()))
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Map(customer);
    }

    public async Task DeleteAsync(Guid customerId, CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);
        Customer? customer = await _customerRepository.GetByIdAsync(
            customerId,
            includeDeleted: true,
            cancellationToken);

        if (customer is null)
        {
            throw CreateNotFoundException(customerId);
        }

        if (customer.Delete(_timeProvider.GetUtcNow()))
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Customer> GetExistingCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        Customer? customer = await _customerRepository.GetByIdAsync(
            customerId,
            includeDeleted: false,
            cancellationToken);

        return customer ?? throw CreateNotFoundException(customerId);
    }

    private static CustomerResponse Map(Customer customer)
    {
        return new CustomerResponse(
            customer.Id,
            customer.Name,
            customer.Gender,
            customer.Age,
            customer.Identification,
            customer.Address,
            customer.Phone,
            customer.IsActive,
            customer.AggregateVersion,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc);
    }

    private static string NormalizeIdentification(string identification)
    {
        return identification?.Trim() ?? string.Empty;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length is < MinimumPasswordLength or > MaximumPasswordLength)
        {
            throw new ValidationException(
                "customer_invalid_password",
                $"Password must contain between {MinimumPasswordLength} and {MaximumPasswordLength} characters.");
        }
    }

    private static void ValidateCustomerId(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ValidationException("customer_invalid_id", "The customer identifier is invalid.");
        }
    }

    private static NotFoundException CreateNotFoundException(Guid customerId)
    {
        return new NotFoundException("customer_not_found", $"Customer '{customerId}' was not found.");
    }
}
