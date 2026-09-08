using Devsu.Customers.Infrastructure.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devsu.Customers.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.EventId);

        builder.Property(message => message.EventId)
            .ValueGeneratedNever();

        builder.Property(message => message.EventType)
            .HasMaxLength(OutboxMessage.MaximumEventTypeLength)
            .IsRequired();

        builder.Property(message => message.OccurredAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(message => message.CorrelationId)
            .HasMaxLength(OutboxMessage.MaximumCorrelationIdLength)
            .IsRequired();

        builder.Property(message => message.AggregateId)
            .IsRequired();

        builder.Property(message => message.AggregateVersion)
            .IsRequired();

        builder.Property(message => message.Payload)
            .IsRequired();

        builder.Property(message => message.AttemptCount)
            .IsRequired();

        builder.Property(message => message.NextAttemptAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(message => message.LastAttemptAtUtc)
            .HasPrecision(7);

        builder.Property(message => message.PublishedAtUtc)
            .HasPrecision(7);

        builder.Property(message => message.LeasedUntilUtc)
            .HasPrecision(7);

        builder.Property(message => message.LastError)
            .HasMaxLength(OutboxMessage.MaximumErrorLength);

        builder.Property(message => message.RowVersion)
            .IsRowVersion();

        builder.HasIndex(message => new
        {
            message.PublishedAtUtc,
            message.NextAttemptAtUtc,
            message.LeasedUntilUtc,
        })
            .HasDatabaseName("IX_OutboxMessages_Pending")
            .HasFilter("[PublishedAtUtc] IS NULL");

        builder.HasIndex(message => new { message.AggregateId, message.AggregateVersion })
            .HasDatabaseName("IX_OutboxMessages_Aggregate");

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_OutboxMessages_AggregateVersion",
                "[AggregateVersion] >= 1");
            tableBuilder.HasCheckConstraint(
                "CK_OutboxMessages_AttemptCount",
                "[AttemptCount] >= 0");
        });
    }
}
