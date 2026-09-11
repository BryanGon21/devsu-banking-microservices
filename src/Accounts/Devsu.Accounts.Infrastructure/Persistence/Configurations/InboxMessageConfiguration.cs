using Devsu.Accounts.Infrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devsu.Accounts.Infrastructure.Persistence.Configurations;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        builder.HasKey(message => message.EventId);

        builder.Property(message => message.EventId)
            .ValueGeneratedNever();

        builder.Property(message => message.EventType)
            .HasMaxLength(InboxMessage.MaximumEventTypeLength)
            .IsRequired();

        builder.Property(message => message.CorrelationId)
            .HasMaxLength(InboxMessage.MaximumCorrelationIdLength)
            .IsRequired();

        builder.Property(message => message.AggregateId)
            .IsRequired();

        builder.Property(message => message.AggregateVersion)
            .IsRequired();

        builder.Property(message => message.OccurredAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(message => message.ProcessedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.HasIndex(message => new { message.AggregateId, message.AggregateVersion })
            .HasDatabaseName("IX_InboxMessages_Aggregate");

        builder.HasIndex(message => message.ProcessedAtUtc)
            .HasDatabaseName("IX_InboxMessages_ProcessedAtUtc");

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_InboxMessages_AggregateVersion",
                "[AggregateVersion] >= 1");
        });
    }
}
