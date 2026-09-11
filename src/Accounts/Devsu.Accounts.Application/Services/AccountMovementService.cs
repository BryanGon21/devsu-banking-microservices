using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devsu.Accounts.Application.Contracts;
using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.Application.Services;

public sealed class AccountMovementService : IAccountMovementService
{
    private const int MaximumPageSize = 100;
    private const int MaximumDateRangeDays = 366;

    private readonly IAccountMovementWriter _movementWriter;
    private readonly IAccountMovementReader _movementReader;
    private readonly IAccountMovementCorrector _movementCorrector;
    private readonly TimeProvider _timeProvider;

    public AccountMovementService(
        IAccountMovementWriter movementWriter,
        IAccountMovementReader movementReader,
        IAccountMovementCorrector movementCorrector,
        TimeProvider timeProvider)
    {
        _movementWriter = movementWriter;
        _movementReader = movementReader;
        _movementCorrector = movementCorrector;
        _timeProvider = timeProvider;
    }

    public async Task<CreateAccountMovementResponse> CreateAsync(
        CreateAccountMovementRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string normalizedAccountNumber = NormalizeAccountNumber(request.AccountNumber);
        string validatedIdempotencyKey = ValidateIdempotencyKey(idempotencyKey);
        MovementAmount amount = MovementAmount.Create(request.Type, request.Value);
        string requestFingerprint = CreateRequestFingerprint(normalizedAccountNumber, amount);

        AccountMovementWriteResult result = await _movementWriter.CreateAsync(
            normalizedAccountNumber,
            amount,
            validatedIdempotencyKey,
            requestFingerprint,
            _timeProvider.GetUtcNow(),
            cancellationToken);

        return new CreateAccountMovementResponse(Map(result.Movement), result.WasCreated);
    }

    public async Task<AccountMovementResponse> GetByIdAsync(
        long movementId,
        CancellationToken cancellationToken)
    {
        ValidateMovementId(movementId);

        AccountMovement movement = await _movementReader.GetByIdAsync(
            movementId,
            cancellationToken)
            ?? throw new NotFoundException(
                "movement_not_found",
                "The requested movement does not exist.");

        return Map(movement);
    }

    public async Task<AccountMovementResponse> CorrectAsync(
        long movementId,
        CorrectAccountMovementRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateMovementId(movementId);

        MovementAmount amount = MovementAmount.Create(request.Type, request.Value);
        string normalizedReason = ValidateAndNormalizeReason(request.Reason);
        string validatedCorrelationId = ValidateCorrelationId(correlationId);

        AccountMovementCorrectionResult result = await _movementCorrector.CorrectAsync(
            movementId,
            amount,
            request.OccurredAtUtc.ToUniversalTime(),
            normalizedReason,
            validatedCorrelationId,
            _timeProvider.GetUtcNow(),
            cancellationToken);

        return Map(result.Movement);
    }

    public async Task<PageResponse<AccountMovementResponse>> ListAsync(
        int page,
        int pageSize,
        string? accountNumber,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        ValidatePagination(page, pageSize);
        string? normalizedAccountNumber = string.IsNullOrWhiteSpace(accountNumber)
            ? null
            : NormalizeAccountNumber(accountNumber);
        (DateTimeOffset? startInclusiveUtc, DateTimeOffset? endExclusiveUtc) =
            ValidateAndConvertDateRange(startDate, endDate);

        if (page - 1 > int.MaxValue / pageSize)
        {
            throw new ValidationException(
                "page_out_of_range",
                "The requested page is outside the supported range.");
        }

        int skip = (page - 1) * pageSize;
        (IReadOnlyCollection<AccountMovement> movements, int totalItems) =
            await _movementReader.ListAsync(
                skip,
                pageSize,
                normalizedAccountNumber,
                startInclusiveUtc,
                endExclusiveUtc,
                cancellationToken);
        int totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        return new PageResponse<AccountMovementResponse>(
            movements.Select(Map).ToArray(),
            page,
            pageSize,
            totalItems,
            totalPages);
    }

    private static string NormalizeAccountNumber(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new ValidationException(
                "account_number_required",
                "Account number is required.");
        }

        string normalizedAccountNumber = accountNumber.Trim();
        if (normalizedAccountNumber.Length > Account.MaximumNumberLength ||
            normalizedAccountNumber.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new ValidationException(
                "account_number_invalid",
                $"Account number must contain only digits and cannot exceed {Account.MaximumNumberLength} characters.");
        }

        return normalizedAccountNumber;
    }

    private static void ValidateMovementId(long movementId)
    {
        if (movementId < 1)
        {
            throw new ValidationException(
                "movement_id_invalid",
                "Movement identifier must be greater than zero.");
        }
    }

    private static string ValidateAndNormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException(
                "movement_correction_reason_required",
                "Correction reason is required.");
        }

        string normalizedReason = reason.Trim();
        if (normalizedReason.Length > AccountMovementCorrection.MaximumReasonLength)
        {
            throw new ValidationException(
                "movement_correction_reason_too_long",
                $"Correction reason cannot exceed {AccountMovementCorrection.MaximumReasonLength} characters.");
        }

        return normalizedReason;
    }

    private static string ValidateCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId) ||
            correlationId.Length > AccountMovementCorrection.MaximumCorrelationIdLength ||
            correlationId.Any(char.IsControl))
        {
            throw new ValidationException(
                "correlation_id_invalid",
                $"Correlation identifier is required, cannot exceed {AccountMovementCorrection.MaximumCorrelationIdLength} characters, or contain control characters.");
        }

        return correlationId;
    }

    private static string ValidateIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException(
                "idempotency_key_required",
                "The Idempotency-Key header is required.");
        }

        if (idempotencyKey.Length > AccountMovement.MaximumIdempotencyKeyLength ||
            idempotencyKey.Any(char.IsControl) ||
            !string.Equals(idempotencyKey, idempotencyKey.Trim(), StringComparison.Ordinal))
        {
            throw new ValidationException(
                "idempotency_key_invalid",
                $"Idempotency-Key cannot exceed {AccountMovement.MaximumIdempotencyKeyLength} characters, contain control characters, or have surrounding whitespace.");
        }

        return idempotencyKey;
    }

    private static string CreateRequestFingerprint(
        string accountNumber,
        MovementAmount amount)
    {
        string canonicalPayload = string.Create(
            CultureInfo.InvariantCulture,
            $"{accountNumber}\n{(int)amount.Type}\n{amount.Value:0.00}");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload));
        return Convert.ToHexString(hash);
    }

    private static (DateTimeOffset? StartInclusiveUtc, DateTimeOffset? EndExclusiveUtc)
        ValidateAndConvertDateRange(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate.HasValue && endDate.HasValue)
        {
            if (startDate.Value > endDate.Value)
            {
                throw new ValidationException(
                    "date_range_invalid",
                    "Start date cannot be later than end date.");
            }

            if (endDate.Value.DayNumber - startDate.Value.DayNumber + 1 > MaximumDateRangeDays)
            {
                throw new ValidationException(
                    "date_range_too_large",
                    $"Date range cannot exceed {MaximumDateRangeDays} days.");
            }
        }

        if (endDate == DateOnly.MaxValue)
        {
            throw new ValidationException(
                "end_date_out_of_range",
                "End date is outside the supported range.");
        }

        DateTimeOffset? startInclusiveUtc = startDate.HasValue
            ? new DateTimeOffset(startDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
        DateTimeOffset? endExclusiveUtc = endDate.HasValue
            ? new DateTimeOffset(endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;

        return (startInclusiveUtc, endExclusiveUtc);
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

    private static AccountMovementResponse Map(AccountMovement movement)
    {
        return new AccountMovementResponse(
            movement.Id,
            movement.AccountNumber,
            movement.OccurredAtUtc,
            movement.Type,
            movement.Value,
            movement.Balance,
            movement.CreatedAtUtc,
            movement.UpdatedAtUtc);
    }
}
