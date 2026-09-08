using Devsu.Customers.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devsu.Customers.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.Property(customer => customer.PasswordHash)
            .HasMaxLength(Customer.MaximumPasswordHashLength)
            .IsRequired();

        builder.Property(customer => customer.IsActive)
            .IsRequired();

        builder.Property(customer => customer.CreatedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(customer => customer.UpdatedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(customer => customer.DeletedAtUtc)
            .HasPrecision(7);

        builder.Property(customer => customer.AggregateVersion)
            .IsRequired();

        builder.Property(customer => customer.RowVersion)
            .IsRowVersion();

        builder.Ignore(customer => customer.IsDeleted);
        builder.Ignore(customer => customer.DomainEvents);

        builder.HasIndex(customer => new { customer.IsActive, customer.DeletedAtUtc })
            .HasDatabaseName("IX_Customers_IsActive_DeletedAtUtc");

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Customers_AggregateVersion",
                "[AggregateVersion] >= 1");
        });
    }
}
