using Devsu.Accounts.Domain.Exceptions;

namespace Devsu.Accounts.Domain.Entities;

public sealed class CustomerProjection
{
    public const int MaximumNameLength = 100;

    private CustomerProjection()
    {
    }

    private CustomerProjection(
        Guid customerId,
        string name,
        bool isActive,
        bool isDeleted,
        long aggregateVersion,
        DateTimeOffset updatedAtUtc)
    {
        CustomerId = ValidateCustomerId(customerId);
        Name = ValidateName(name);
        ValidateStatus(isActive, isDeleted);
        ValidateVersion(aggregateVersion);
        IsActive = isActive;
        IsDeleted = isDeleted;
        AggregateVersion = aggregateVersion;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public Guid CustomerId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool IsDeleted { get; private set; }

    public long AggregateVersion { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsAvailable => IsActive && !IsDeleted;

    public static CustomerProjection Create(
        Guid customerId,
        string name,
        bool isActive,
        bool isDeleted,
        long aggregateVersion,
        DateTimeOffset updatedAtUtc)
    {
        return new CustomerProjection(
            customerId,
            name,
            isActive,
            isDeleted,
            aggregateVersion,
            updatedAtUtc);
    }

    public bool ApplySnapshot(
        string name,
        bool isActive,
        bool isDeleted,
        long aggregateVersion,
        DateTimeOffset updatedAtUtc)
    {
        if (aggregateVersion <= AggregateVersion)
        {
            return false;
        }

        if (IsDeleted && !isDeleted)
        {
            throw new BusinessRuleException(
                "customer_projection_cannot_reactivate_deleted",
                "A deleted customer projection cannot be reactivated.");
        }

        string validatedName = ValidateName(name);
        ValidateStatus(isActive, isDeleted);
        ValidateVersion(aggregateVersion);

        Name = validatedName;
        IsActive = isActive;
        IsDeleted = isDeleted;
        AggregateVersion = aggregateVersion;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        return true;
    }

    private static Guid ValidateCustomerId(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "customer_projection_id_invalid",
                "Customer projection identifier is invalid.");
        }

        return customerId;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException(
                "customer_projection_name_required",
                "Customer projection name is required.");
        }

        string normalizedName = name.Trim();
        if (normalizedName.Length > MaximumNameLength)
        {
            throw new BusinessRuleException(
                "customer_projection_name_invalid",
                $"Customer projection name cannot exceed {MaximumNameLength} characters.");
        }

        return normalizedName;
    }

    private static void ValidateStatus(bool isActive, bool isDeleted)
    {
        if (isActive && isDeleted)
        {
            throw new BusinessRuleException(
                "customer_projection_status_invalid",
                "A deleted customer projection cannot be active.");
        }
    }

    private static void ValidateVersion(long aggregateVersion)
    {
        if (aggregateVersion < 1)
        {
            throw new BusinessRuleException(
                "customer_projection_version_invalid",
                "Customer projection version must be greater than zero.");
        }
    }
}
