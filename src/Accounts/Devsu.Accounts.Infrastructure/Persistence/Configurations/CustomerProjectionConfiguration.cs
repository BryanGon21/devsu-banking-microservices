using Devsu.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devsu.Accounts.Infrastructure.Persistence.Configurations;

internal sealed class CustomerProjectionConfiguration : IEntityTypeConfiguration<CustomerProjection>
{
    public void Configure(EntityTypeBuilder<CustomerProjection> builder)
    {
        builder.ToTable("CustomerProjections");
        builder.HasKey(customer => customer.CustomerId);

        builder.Property(customer => customer.CustomerId)
            .ValueGeneratedNever();

        builder.Property(customer => customer.Name)
            .HasMaxLength(CustomerProjection.MaximumNameLength)
            .IsRequired();

        builder.Property(customer => customer.IsActive)
            .IsRequired();

        builder.Property(customer => customer.IsDeleted)
            .IsRequired();

        builder.Property(customer => customer.AggregateVersion)
            .IsRequired();

        builder.Property(customer => customer.UpdatedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(customer => customer.RowVersion)
            .IsRowVersion();

        builder.HasIndex(customer => new { customer.IsActive, customer.IsDeleted })
            .HasDatabaseName("IX_CustomerProjections_Status");

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_CustomerProjections_AggregateVersion",
                "[AggregateVersion] >= 1");
            tableBuilder.HasCheckConstraint(
                "CK_CustomerProjections_Status",
                "NOT ([IsActive] = 1 AND [IsDeleted] = 1)");
        });
    }
}
