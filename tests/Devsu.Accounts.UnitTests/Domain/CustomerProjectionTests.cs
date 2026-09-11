using Devsu.Accounts.Domain.Entities;
using Devsu.Accounts.Domain.Exceptions;

namespace Devsu.Accounts.UnitTests.Domain;

public sealed class CustomerProjectionTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 2, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApplySnapshot_WithNewerVersion_UpdatesProjection()
    {
        CustomerProjection projection = CreateProjection(version: 1);

        bool applied = projection.ApplySnapshot(
            "Jose Lema Updated",
            false,
            false,
            2,
            InitialTime.AddMinutes(1));

        Assert.True(applied);
        Assert.Equal("Jose Lema Updated", projection.Name);
        Assert.False(projection.IsActive);
        Assert.Equal(2, projection.AggregateVersion);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(1)]
    public void ApplySnapshot_WithDuplicateOrOlderVersion_DoesNotRevertProjection(long version)
    {
        CustomerProjection projection = CreateProjection(version: 2);

        bool applied = projection.ApplySnapshot(
            "Stale Name",
            false,
            false,
            version,
            InitialTime.AddMinutes(1));

        Assert.False(applied);
        Assert.Equal("Jose Lema", projection.Name);
        Assert.True(projection.IsActive);
        Assert.Equal(2, projection.AggregateVersion);
        Assert.Equal(InitialTime, projection.UpdatedAtUtc);
    }

    [Fact]
    public void ApplySnapshot_AfterDeletionCannotReactivateCustomer()
    {
        CustomerProjection projection = CustomerProjection.Create(
            Guid.NewGuid(),
            "Jose Lema",
            false,
            true,
            2,
            InitialTime);

        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            projection.ApplySnapshot(
                "Jose Lema",
                true,
                false,
                3,
                InitialTime.AddMinutes(1)));

        Assert.Equal("customer_projection_cannot_reactivate_deleted", exception.Code);
    }

    private static CustomerProjection CreateProjection(long version)
    {
        return CustomerProjection.Create(
            Guid.NewGuid(),
            "Jose Lema",
            true,
            false,
            version,
            InitialTime);
    }
}
