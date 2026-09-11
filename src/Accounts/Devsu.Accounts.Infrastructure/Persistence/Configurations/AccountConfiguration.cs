using Devsu.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devsu.Accounts.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(account => account.Number);

        builder.Property(account => account.Number)
            .HasMaxLength(Account.MaximumNumberLength)
            .ValueGeneratedNever();

        builder.Property(account => account.Type)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(account => account.InitialBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(account => account.CurrentBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(account => account.IsActive)
            .IsRequired();

        builder.Property(account => account.CustomerId)
            .IsRequired();

        builder.Property(account => account.CreatedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(account => account.UpdatedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(account => account.RowVersion)
            .IsRowVersion();

        builder.HasOne<CustomerProjection>()
            .WithMany()
            .HasForeignKey(account => account.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(account => new { account.CustomerId, account.IsActive })
            .HasDatabaseName("IX_Accounts_CustomerId_IsActive");

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Accounts_Number",
                "[Number] <> '' AND [Number] NOT LIKE '%[^0-9]%'");
            tableBuilder.HasCheckConstraint(
                "CK_Accounts_Type",
                "[Type] IN ('Savings', 'Checking')");
            tableBuilder.HasCheckConstraint(
                "CK_Accounts_InitialBalance",
                "[InitialBalance] >= 0");
            tableBuilder.HasCheckConstraint(
                "CK_Accounts_CurrentBalance",
                "[CurrentBalance] >= 0");
        });
    }
}
