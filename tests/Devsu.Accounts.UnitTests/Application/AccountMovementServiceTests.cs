using Devsu.Accounts.Application.Contracts;
using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Application.Services;
using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.Enums;
using Devsu.Accounts.Domain.ValueObjects;

namespace Devsu.Accounts.UnitTests.Application;

public sealed class AccountMovementServiceTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_WithValidRequest_PassesCanonicalDataToWriter()
    {
        FakeMovementWriter writer = new();
        AccountMovementService service = CreateService(writer, new FakeMovementReader());

        CreateAccountMovementResponse result = await service.CreateAsync(
            new CreateAccountMovementRequest(" 001234 ", MovementType.Deposit, 50m),
            "request-1",
            CancellationToken.None);

        Assert.True(result.WasCreated);
        Assert.Equal("001234", writer.AccountNumber);
        Assert.Equal("request-1", writer.IdempotencyKey);
        Assert.Equal(64, writer.RequestFingerprint?.Length);
        Assert.Equal(50m, result.Movement.Value);
        Assert.Equal(CurrentTime, result.Movement.OccurredAtUtc);
    }

    [Fact]
    public async Task Create_ReplayedRequest_ReturnsExistingResult()
    {
        FakeMovementWriter writer = new() { WasCreated = false };
        AccountMovementService service = CreateService(writer, new FakeMovementReader());

        CreateAccountMovementResponse result = await service.CreateAsync(
            new CreateAccountMovementRequest("001234", MovementType.Deposit, 50m),
            "request-1",
            CancellationToken.None);

        Assert.False(result.WasCreated);
    }

    [Fact]
    public async Task Create_EquivalentDecimalValues_ProduceSameFingerprint()
    {
        FakeMovementWriter firstWriter = new();
        FakeMovementWriter secondWriter = new();
        AccountMovementService firstService = CreateService(firstWriter, new FakeMovementReader());
        AccountMovementService secondService = CreateService(secondWriter, new FakeMovementReader());

        await firstService.CreateAsync(
            new CreateAccountMovementRequest("001234", MovementType.Deposit, 50m),
            "request-1",
            CancellationToken.None);
        await secondService.CreateAsync(
            new CreateAccountMovementRequest("001234", MovementType.Deposit, 50.00m),
            "request-1",
            CancellationToken.None);

        Assert.Equal(firstWriter.RequestFingerprint, secondWriter.RequestFingerprint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutIdempotencyKey_IsRejected(string? idempotencyKey)
    {
        AccountMovementService service = CreateService(
            new FakeMovementWriter(),
            new FakeMovementReader());

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(
                new CreateAccountMovementRequest("001234", MovementType.Deposit, 50m),
                idempotencyKey,
                CancellationToken.None));

        Assert.Equal("idempotency_key_required", exception.Code);
    }

    [Fact]
    public async Task GetById_WhenMissing_ThrowsNotFound()
    {
        AccountMovementService service = CreateService(
            new FakeMovementWriter(),
            new FakeMovementReader());

        NotFoundException exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetByIdAsync(99, CancellationToken.None));

        Assert.Equal("movement_not_found", exception.Code);
    }

    [Fact]
    public async Task List_WithRangeOver366Days_IsRejected()
    {
        AccountMovementService service = CreateService(
            new FakeMovementWriter(),
            new FakeMovementReader());

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ListAsync(
                1,
                20,
                null,
                new DateOnly(2024, 1, 1),
                new DateOnly(2025, 1, 1),
                CancellationToken.None));

        Assert.Equal("date_range_too_large", exception.Code);
    }

    private static AccountMovementService CreateService(
        FakeMovementWriter writer,
        FakeMovementReader reader)
    {
        return new AccountMovementService(
            writer,
            reader,
            new FixedTimeProvider(CurrentTime));
    }

    private sealed class FakeMovementWriter : IAccountMovementWriter
    {
        public bool WasCreated { get; init; } = true;

        public string? AccountNumber { get; private set; }

        public string? IdempotencyKey { get; private set; }

        public string? RequestFingerprint { get; private set; }

        public Task<AccountMovementWriteResult> CreateAsync(
            string accountNumber,
            MovementAmount amount,
            string idempotencyKey,
            string requestFingerprint,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            AccountNumber = accountNumber;
            IdempotencyKey = idempotencyKey;
            RequestFingerprint = requestFingerprint;
            AccountMovement movement = AccountMovement.Create(
                accountNumber,
                amount,
                150m,
                idempotencyKey,
                requestFingerprint,
                occurredAtUtc);

            return Task.FromResult(new AccountMovementWriteResult(movement, WasCreated));
        }
    }

    private sealed class FakeMovementReader : IAccountMovementReader
    {
        public Task<AccountMovement?> GetByIdAsync(
            long movementId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<AccountMovement?>(null);
        }

        public Task<(IReadOnlyCollection<AccountMovement> Items, int TotalItems)> ListAsync(
            int skip,
            int take,
            string? accountNumber,
            DateTimeOffset? startInclusiveUtc,
            DateTimeOffset? endExclusiveUtc,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<(IReadOnlyCollection<AccountMovement>, int)>(([], 0));
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
