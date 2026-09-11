using Devsu.Customers.Domain.Entities;
using Devsu.Customers.Domain.Enums;
using Devsu.Customers.Domain.Events;
using Devsu.Customers.Domain.Exceptions;

namespace Devsu.Customers.UnitTests.Domain;

public sealed class CustomerTests
{
    private static readonly DateTimeOffset CreationDate =
        new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_BuildsCustomerAndNormalizesText()
    {
        Customer customer = CreateCustomer();

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Jose Lema", customer.Name);
        Assert.Equal("1002003004", customer.Identification);
        Assert.Equal("123 Main Street", customer.Address);
        Assert.Equal("098254785", customer.Phone);
        Assert.Equal(Gender.Male, customer.Gender);
        Assert.Equal(35, customer.Age);
        Assert.True(customer.IsActive);
        Assert.False(customer.IsDeleted);
        Assert.Equal(1, customer.AggregateVersion);
        Assert.Equal(CreationDate, customer.CreatedAtUtc);
        Assert.Equal(CreationDate, customer.UpdatedAtUtc);
    }

    [Fact]
    public void Create_RaisesVersionedCustomerCreatedEvent()
    {
        Customer customer = CreateCustomer();

        CustomerCreatedDomainEvent domainEvent =
            Assert.IsType<CustomerCreatedDomainEvent>(Assert.Single(customer.DomainEvents));
        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
        Assert.Equal(customer.Id, domainEvent.CustomerId);
        Assert.Equal(customer.Name, domainEvent.Name);
        Assert.Equal(1, domainEvent.AggregateVersion);
        Assert.True(domainEvent.IsActive);
        Assert.False(domainEvent.IsDeleted);
        Assert.Equal(CreationDate, domainEvent.OccurredAtUtc);
    }

    [Fact]
    public void Create_CustomerIsAPerson()
    {
        Customer customer = CreateCustomer();

        Assert.IsAssignableFrom<Person>(customer);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(131)]
    public void Create_WithAgeOutsideAllowedRange_ThrowsBusinessRule(int age)
    {
        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            CreateCustomer(age: age));

        Assert.Equal("customer_invalid_age", exception.Code);
    }

    [Theory]
    [InlineData(Gender.Unspecified)]
    [InlineData((Gender)999)]
    public void Create_WithInvalidGender_ThrowsBusinessRule(Gender gender)
    {
        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            CreateCustomer(gender: gender));

        Assert.Equal("customer_invalid_gender", exception.Code);
    }

    [Fact]
    public void Create_WithoutName_ThrowsBusinessRule()
    {
        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            CreateCustomer(name: "   "));

        Assert.Equal("customer_required_field", exception.Code);
    }

    [Fact]
    public void ChangeStatus_WithDifferentStatus_IncrementsVersionAndTimestamp()
    {
        Customer customer = CreateCustomer();
        customer.ClearDomainEvents();
        DateTimeOffset updateDate = CreationDate.AddHours(1);

        bool changed = customer.ChangeStatus(false, updateDate);

        Assert.True(changed);
        Assert.False(customer.IsActive);
        Assert.Equal(2, customer.AggregateVersion);
        Assert.Equal(updateDate, customer.UpdatedAtUtc);
        CustomerUpdatedDomainEvent domainEvent =
            Assert.IsType<CustomerUpdatedDomainEvent>(Assert.Single(customer.DomainEvents));
        Assert.False(domainEvent.IsActive);
        Assert.Equal(2, domainEvent.AggregateVersion);
    }

    [Fact]
    public void ChangeStatus_WithSameStatus_DoesNotChangeVersion()
    {
        Customer customer = CreateCustomer();
        customer.ClearDomainEvents();

        bool changed = customer.ChangeStatus(true, CreationDate.AddHours(1));

        Assert.False(changed);
        Assert.Equal(1, customer.AggregateVersion);
        Assert.Equal(CreationDate, customer.UpdatedAtUtc);
        Assert.Empty(customer.DomainEvents);
    }

    [Fact]
    public void Delete_Twice_IsIdempotent()
    {
        Customer customer = CreateCustomer();
        customer.ClearDomainEvents();
        DateTimeOffset deletionDate = CreationDate.AddDays(1);

        bool firstChange = customer.Delete(deletionDate);
        bool secondChange = customer.Delete(deletionDate.AddMinutes(1));

        Assert.True(firstChange);
        Assert.False(secondChange);
        Assert.True(customer.IsDeleted);
        Assert.False(customer.IsActive);
        Assert.Equal(deletionDate, customer.DeletedAtUtc);
        Assert.Equal(2, customer.AggregateVersion);
        CustomerDeletedDomainEvent domainEvent =
            Assert.IsType<CustomerDeletedDomainEvent>(Assert.Single(customer.DomainEvents));
        Assert.False(domainEvent.IsActive);
        Assert.True(domainEvent.IsDeleted);
        Assert.Equal(2, domainEvent.AggregateVersion);
    }

    [Fact]
    public void Update_AfterDeletion_ThrowsBusinessRule()
    {
        Customer customer = CreateCustomer();
        customer.Delete(CreationDate.AddDays(1));

        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            customer.Update(
                "Jose Lema",
                Gender.Male,
                35,
                "1002003004",
                "New address",
                "098254785",
                null,
                true,
                CreationDate.AddDays(2)));

        Assert.Equal("customer_deleted", exception.Code);
    }

    [Fact]
    public void Update_WithoutChanges_IsNoOp()
    {
        Customer customer = CreateCustomer();
        customer.ClearDomainEvents();

        bool changed = customer.Update(
            customer.Name,
            customer.Gender,
            customer.Age,
            customer.Identification,
            customer.Address,
            customer.Phone,
            null,
            customer.IsActive,
            CreationDate.AddHours(1));

        Assert.False(changed);
        Assert.Equal(1, customer.AggregateVersion);
        Assert.Equal(CreationDate, customer.UpdatedAtUtc);
        Assert.Empty(customer.DomainEvents);
    }

    [Fact]
    public void Update_WithNewData_ChangesAggregateOnlyOnce()
    {
        Customer customer = CreateCustomer();
        customer.ClearDomainEvents();
        DateTimeOffset updateDate = CreationDate.AddHours(1);

        bool changed = customer.Update(
            "Jose Lema Updated",
            Gender.Male,
            36,
            "1002003004",
            "Quito",
            "099999999",
            "new-password-hash",
            false,
            updateDate);

        Assert.True(changed);
        Assert.Equal("Jose Lema Updated", customer.Name);
        Assert.Equal(36, customer.Age);
        Assert.Equal("new-password-hash", customer.PasswordHash);
        Assert.False(customer.IsActive);
        Assert.Equal(2, customer.AggregateVersion);
        Assert.Equal(updateDate, customer.UpdatedAtUtc);
        CustomerUpdatedDomainEvent domainEvent =
            Assert.IsType<CustomerUpdatedDomainEvent>(Assert.Single(customer.DomainEvents));
        Assert.Equal("Jose Lema Updated", domainEvent.Name);
        Assert.Equal(2, domainEvent.AggregateVersion);
    }

    private static Customer CreateCustomer(
        string name = "  Jose Lema  ",
        Gender gender = Gender.Male,
        int age = 35)
    {
        return Customer.Create(
            Guid.NewGuid(),
            name,
            gender,
            age,
            " 1002003004 ",
            " 123 Main Street ",
            " 098254785 ",
            "secure-password-hash",
            true,
            CreationDate);
    }
}
