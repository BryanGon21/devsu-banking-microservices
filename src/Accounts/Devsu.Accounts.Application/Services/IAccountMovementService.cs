using Devsu.Accounts.Application.Contracts;

namespace Devsu.Accounts.Application.Services;

public interface IAccountMovementService
{
    public Task<CreateAccountMovementResponse> CreateAsync(
        CreateAccountMovementRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    public Task<AccountMovementResponse> GetByIdAsync(
        long movementId,
        CancellationToken cancellationToken);

    public Task<AccountMovementResponse> CorrectAsync(
        long movementId,
        CorrectAccountMovementRequest request,
        string correlationId,
        CancellationToken cancellationToken);

    public Task<PageResponse<AccountMovementResponse>> ListAsync(
        int page,
        int pageSize,
        string? accountNumber,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken);
}
