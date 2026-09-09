using Devsu.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devsu.Accounts.Infrastructure.Persistence.Configurations;

internal sealed class AccountMovementConfiguration : IEntityTypeConfiguration<AccountMovement>
{
    public void Configure(EntityTypeBuilder<AccountMovement> builder)
    {
        builder.ToTable("AccountMovements");
        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Id)
            .ValueGeneratedOnAdd();

        builder.Property(movement => movement.AccountNumber)
            .HasMaxLength(Account.MaximumNumberLength)
            .IsRequired();

        builder.Property(movement => movement.Type)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(movement => movement.Value)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.Balance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(movement => movement.IdempotencyKey)
            .HasMaxLength(AccountMovement.MaximumIdempotencyKeyLength)
            .UseCollation("Latin1_General_100_BIN2")
            .IsRequired();

        builder.Property(movement => movement.RequestFingerprint)
            .HasMaxLength(AccountMovement.RequestFingerprintLength)
            .IsUnicode(false)
            .IsFixedLength()
            .IsRequired();

        builder.Property(movement => movement.OccurredAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(movement => movement.CreatedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(movement => movement.UpdatedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(movement => movement.RowVersion)
            .IsRowVersion();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(movement => movement.AccountNumber)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(movement => movement.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_AccountMovements_IdempotencyKey");

        builder.HasIndex(movement => new
        {
            movement.AccountNumber,
            movement.OccurredAtUtc,
            movement.Id,
        })
            .HasDatabaseName("IX_AccountMovements_AccountNumber_OccurredAtUtc_Id");

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AccountMovements_Type",
                "[Type] IN ('Deposit', 'Withdrawal')");
            tableBuilder.HasCheckConstraint(
                "CK_AccountMovements_Value",
                "([Type] = 'Deposit' AND [Value] > 0) OR " +
                "([Type] = 'Withdrawal' AND [Value] < 0)");
            tableBuilder.HasCheckConstraint(
                "CK_AccountMovements_Balance",
                "[Balance] >= 0");
        });
    }
}
