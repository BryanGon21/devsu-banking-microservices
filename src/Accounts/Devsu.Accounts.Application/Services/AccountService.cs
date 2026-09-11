using Devsu.Accounts.Application.Contracts;
using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Domain.Entities;

namespace Devsu.Accounts.Application.Services;

public sealed class AccountService : IAccountService
{
    private const int MaximumPageSize = 100;

    private readonly IAccountRepository _accountRepository;
    private readonly ICustomerProjectionReader _customerProjectionReader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AccountService(
        IAccountRepository accountRepository,
        ICustomerProjectionReader customerProjectionReader,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _accountRepository = accountRepository;
        _customerProjectionReader = customerProjectionReader;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<AccountResponse> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Account account = Account.Create(
            request.Number,
            request.Type,
            request.InitialBalance,
            request.IsActive,
            request.CustomerId,
            _timeProvider.GetUtcNow());

        if (await _accountRepository.ExistsAsync(account.Number, cancellationToken))
        {
            throw new ConflictException(
                "account_duplicate_number",
                "An account with the specified number already exists.");
        }

        CustomerProjection? customer = await _customerProjectionReader.GetByIdAsync(
            account.CustomerId,
            cancellationToken);
        if (customer is null || !customer.IsAvailable)
        {
            throw new ConflictException(
                "cliente_no_disponible",
                "El cliente todavía no está disponible en este servicio o se encuentra inactivo. Reintente la operación.");
        }

        await _accountRepository.AddAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(account);
    }

    public async Task<AccountResponse> GetByNumberAsync(
        string number,
        CancellationToken cancellationToken)
    {
        string normalizedNumber = NormalizeNumber(number);
        Account account = await FindAccountAsync(normalizedNumber, cancellationToken);
        return Map(account);
    }

    public async Task<PageResponse<AccountResponse>> ListAsync(
        int page,
        int pageSize,
        Guid? customerId,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        ValidatePagination(page, pageSize);
        if (customerId == Guid.Empty)
        {
            throw new ValidationException(
                "customer_id_invalid",
                "Customer identifier filter is invalid.");
        }

        if (page - 1 > int.MaxValue / pageSize)
        {
            throw new ValidationException(
                "page_out_of_range",
                "The requested page is outside the supported range.");
        }

        int skip = (page - 1) * pageSize;

        (IReadOnlyCollection<Account> accounts, int totalItems) =
            await _accountRepository.ListAsync(
                skip,
                pageSize,
                customerId,
                isActive,
                cancellationToken);
        int totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        return new PageResponse<AccountResponse>(
            accounts.Select(Map).ToArray(),
            page,
            pageSize,
            totalItems,
            totalPages);
    }

    public async Task<AccountResponse> UpdateAsync(
        string number,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Account account = await FindAccountAsync(NormalizeNumber(number), cancellationToken);
        bool hasMovements = account.InitialBalance != request.InitialBalance &&
            await _accountRepository.HasMovementsAsync(account.Number, cancellationToken);
        bool changed = account.Update(
            request.Type,
            request.InitialBalance,
            request.IsActive,
            hasMovements,
            _timeProvider.GetUtcNow());

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Map(account);
    }

    public async Task<AccountResponse> ChangeStatusAsync(
        string number,
        ChangeAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Account account = await FindAccountAsync(NormalizeNumber(number), cancellationToken);
        if (account.ChangeStatus(request.IsActive, _timeProvider.GetUtcNow()))
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Map(account);
    }

    private async Task<Account> FindAccountAsync(
        string number,
        CancellationToken cancellationToken)
    {
        return await _accountRepository.GetByNumberAsync(number, cancellationToken)
            ?? throw new NotFoundException("account_not_found", "The requested account does not exist.");
    }

    private static string NormalizeNumber(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ValidationException("account_number_required", "Account number is required.");
        }

        return number.Trim();
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ValidationException("page_invalid", "Page must be greater than zero.");
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ValidationException(
                "page_size_invalid",
                $"Page size must be between 1 and {MaximumPageSize}.");
        }
    }

    private static AccountResponse Map(Account account)
    {
        return new AccountResponse(
            account.Number,
            account.Type,
            account.InitialBalance,
            account.CurrentBalance,
            account.IsActive,
            account.CustomerId,
            account.CreatedAtUtc,
            account.UpdatedAtUtc);
    }
}
