using Devsu.Customers.Domain.Enums;
using Devsu.Customers.Domain.Exceptions;

namespace Devsu.Customers.Domain.Entities;

public sealed class Customer : Person
{
    public const int MaximumPasswordHashLength = 512;

    private Customer()
    {
    }

    private Customer(
        Guid id,
        string name,
        Gender gender,
        int age,
        string identification,
        string address,
        string phone,
        string passwordHash,
        bool isActive,
        DateTimeOffset createdAtUtc)
        : base(id, name, gender, age, identification, address, phone)
    {
        PasswordHash = ValidatePasswordHash(passwordHash);
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
        AggregateVersion = 1;
    }

    public string PasswordHash { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public long AggregateVersion { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted => DeletedAtUtc.HasValue;

    public static Customer Create(
        Guid id,
        string name,
        Gender gender,
        int age,
        string identification,
        string address,
        string phone,
        string passwordHash,
        bool isActive,
        DateTimeOffset createdAtUtc)
    {
        return new Customer(
            id,
            name,
            gender,
            age,
            identification,
            address,
            phone,
            passwordHash,
            isActive,
            createdAtUtc);
    }

    public bool Update(
        string name,
        Gender gender,
        int age,
        string identification,
        string address,
        string phone,
        string? newPasswordHash,
        bool isActive,
        DateTimeOffset updatedAtUtc)
    {
        EnsureNotDeleted();

        bool personalDetailsChanged = UpdatePersonalDetails(
            name,
            gender,
            age,
            identification,
            address,
            phone);

        bool passwordChanged = false;
        if (newPasswordHash is not null)
        {
            string validatedHash = ValidatePasswordHash(newPasswordHash);
            passwordChanged = !string.Equals(PasswordHash, validatedHash, StringComparison.Ordinal);
            PasswordHash = validatedHash;
        }

        bool statusChanged = IsActive != isActive;
        if (!personalDetailsChanged && !passwordChanged && !statusChanged)
        {
            return false;
        }

        IsActive = isActive;
        RecordChange(updatedAtUtc);
        return true;
    }

    public bool ChangeStatus(bool isActive, DateTimeOffset updatedAtUtc)
    {
        EnsureNotDeleted();

        if (IsActive == isActive)
        {
            return false;
        }

        IsActive = isActive;
        RecordChange(updatedAtUtc);
        return true;
    }

    public bool Delete(DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return false;
        }

        IsActive = false;
        DeletedAtUtc = deletedAtUtc.ToUniversalTime();
        RecordChange(deletedAtUtc);
        return true;
    }

    private static string ValidatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > MaximumPasswordHashLength)
        {
            throw new BusinessRuleException(
                "customer_invalid_password_hash",
                "The processed password is invalid.");
        }

        return passwordHash;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new BusinessRuleException(
                "customer_deleted",
                "A deleted customer cannot be modified.");
        }
    }

    private void RecordChange(DateTimeOffset updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        AggregateVersion++;
    }
}
