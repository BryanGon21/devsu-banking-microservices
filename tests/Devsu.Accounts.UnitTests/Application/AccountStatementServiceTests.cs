using Devsu.Accounts.Application.Contracts;
using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Ports;
using Devsu.Accounts.Application.Services;
using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.UnitTests.Application;

public sealed class AccountStatementServiceTests
{
    private static readonly Guid CustomerId =
        Guid.Parse("b91ea38e-9a9d-4ec1-8d0d-1b55c65532aa");

    [Fact]
    public async Task Get_WithMovementsAndEmptyAccount_MapsBalancesAndOrdersResults()
    {
        FakeAccountStatementReader reader = new(CreateStatement());
        AccountStatementService service = new(reader);

        AccountStatementResponse response = await service.GetAsync(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            CustomerId,
            CancellationToken.None);

        Assert.Equal(CustomerId, response.Customer.CustomerId);
        Assert.Equal("Marianela Montalvo", response.Customer.Name);
        Assert.Equal(new DateOnly(2026, 2, 1), response.StartDate);
        Assert.Equal(new DateOnly(2026, 2, 28), response.EndDate);

        AccountStatementAccountResponse firstAccount = response.Accounts.First();
        Assert.Equal("225487", firstAccount.Number);
        Assert.Equal(100m, firstAccount.OpeningBalance);
        Assert.Equal(130m, firstAccount.ClosingBalance);
        Assert.Equal(175m, firstAccount.CurrentBalance);
        Assert.Equal([1L, 2L], firstAccount.Movements.Select(movement => movement.Id));

        AccountStatementAccountResponse emptyAccount = response.Accounts.Last();
        Assert.Equal("496825", emptyAccount.Number);
        Assert.Equal(540m, emptyAccount.OpeningBalance);
        Assert.Equal(540m, emptyAccount.ClosingBalance);
        Assert.Empty(emptyAccount.Movements);
    }

    [Fact]
    public async Task Get_UsesInclusiveUtcDateBoundaries()
    {
        FakeAccountStatementReader reader = new(CreateStatement());
        AccountStatementService service = new(reader);

        await service.GetAsync(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            CustomerId,
            CancellationToken.None);

        Assert.Equal(
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            reader.StartInclusiveUtc);
        Assert.Equal(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            reader.EndExclusiveUtc);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Get_WithMissingParameter_IsRejected(
        bool omitStartDate,
        bool omitEndDate,
        bool omitCustomerId)
    {
        FakeAccountStatementReader reader = new(CreateStatement());
        AccountStatementService service = new(reader);

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetAsync(
                omitStartDate ? null : new DateOnly(2026, 2, 1),
                omitEndDate ? null : new DateOnly(2026, 2, 28),
                omitCustomerId ? null : CustomerId,
                CancellationToken.None));

        Assert.Equal("report_parameters_required", exception.Code);
        Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData("2026-03-01", "2026-02-28", "date_range_invalid")]
    [InlineData("2024-01-01", "2025-01-01", "date_range_too_large")]
    public async Task Get_WithInvalidDateRange_IsRejected(
        string startDate,
        string endDate,
        string expectedCode)
    {
        FakeAccountStatementReader reader = new(CreateStatement());
        AccountStatementService service = new(reader);

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetAsync(
                DateOnly.Parse(startDate, System.Globalization.CultureInfo.InvariantCulture),
                DateOnly.Parse(endDate, System.Globalization.CultureInfo.InvariantCulture),
                CustomerId,
                CancellationToken.None));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task Get_WhenProjectionDoesNotExist_ThrowsNotFound()
    {
        AccountStatementService service = new(new FakeAccountStatementReader(null));

        NotFoundException exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAsync(
                new DateOnly(2026, 2, 1),
                new DateOnly(2026, 2, 28),
                CustomerId,
                CancellationToken.None));

        Assert.Equal("customer_not_found", exception.Code);
    }

    [Fact]
    public async Task Get_WhenCustomerHasNoAccounts_ReturnsEmptyStatement()
    {
        AccountStatementData statement = new(
            CustomerId,
            "Jose Lema",
            []);
        AccountStatementService service = new(new FakeAccountStatementReader(statement));

        AccountStatementResponse response = await service.GetAsync(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            CustomerId,
            CancellationToken.None);

        Assert.Equal(CustomerId, response.Customer.CustomerId);
        Assert.Empty(response.Accounts);
    }

    private static AccountStatementData CreateStatement()
    {
        AccountStatementMovementData firstMovement = new(
            1,
            "225487",
            new DateTimeOffset(2026, 2, 10, 10, 0, 0, TimeSpan.Zero),
            MovementType.Deposit,
            50m,
            150m);
        AccountStatementMovementData secondMovement = new(
            2,
            "225487",
            new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero),
            MovementType.Withdrawal,
            -20m,
            130m);
        AccountStatementAccountData accountWithMovements = new(
            "225487",
            AccountType.Checking,
            100m,
            100m,
            175m,
            true,
            [secondMovement, firstMovement]);
        AccountStatementAccountData emptyAccount = new(
            "496825",
            AccountType.Savings,
            540m,
            540m,
            540m,
            false,
            []);

        return new AccountStatementData(
            CustomerId,
            "Marianela Montalvo",
            [emptyAccount, accountWithMovements]);
    }

    private sealed class FakeAccountStatementReader : IAccountStatementReader
    {
        private readonly AccountStatementData? _statement;

        public FakeAccountStatementReader(AccountStatementData? statement)
        {
            _statement = statement;
        }

        public int Calls { get; private set; }

        public DateTimeOffset StartInclusiveUtc { get; private set; }

        public DateTimeOffset EndExclusiveUtc { get; private set; }

        public Task<AccountStatementData?> GetAsync(
            Guid customerId,
            DateTimeOffset startInclusiveUtc,
            DateTimeOffset endExclusiveUtc,
            CancellationToken cancellationToken)
        {
            Calls++;
            StartInclusiveUtc = startInclusiveUtc;
            EndExclusiveUtc = endExclusiveUtc;
            return Task.FromResult(_statement);
        }
    }
}
