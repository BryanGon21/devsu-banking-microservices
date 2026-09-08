namespace Devsu.Customers.Domain.Events;

public abstract record CustomerDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CustomerId,
    long AggregateVersion,
    string Name,
    bool IsActive,
    bool IsDeleted) : IDomainEvent;

public sealed record CustomerCreatedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CustomerId,
    long AggregateVersion,
    string Name,
    bool IsActive,
    bool IsDeleted)
    : CustomerDomainEvent(
        EventId,
        OccurredAtUtc,
        CustomerId,
        AggregateVersion,
        Name,
        IsActive,
        IsDeleted);

public sealed record CustomerUpdatedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CustomerId,
    long AggregateVersion,
    string Name,
    bool IsActive,
    bool IsDeleted)
    : CustomerDomainEvent(
        EventId,
        OccurredAtUtc,
        CustomerId,
        AggregateVersion,
        Name,
        IsActive,
        IsDeleted);

public sealed record CustomerDeletedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid CustomerId,
    long AggregateVersion,
    string Name,
    bool IsActive,
    bool IsDeleted)
    : CustomerDomainEvent(
        EventId,
        OccurredAtUtc,
        CustomerId,
        AggregateVersion,
        Name,
        IsActive,
        IsDeleted);
