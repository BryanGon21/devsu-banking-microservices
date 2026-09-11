using Devsu.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devsu.Accounts.Infrastructure.Persistence.Configurations;

internal sealed class AccountMovementCorrectionConfiguration :
    IEntityTypeConfiguration<AccountMovementCorrection>
{
    public void Configure(EntityTypeBuilder<AccountMovementCorrection> builder)
    {
        builder.ToTable("AccountMovementCorrections");
        builder.HasKey(correction => correction.Id);

        builder.Property(correction => correction.Id)
            .ValueGeneratedOnAdd();

        builder.Property(correction => correction.MovementId)
            .IsRequired();

        builder.Property(correction => correction.PreviousType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(correction => correction.PreviousValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(correction => correction.PreviousOccurredAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(correction => correction.PreviousBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(correction => correction.NewType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(correction => correction.NewValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(correction => correction.NewOccurredAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.Property(correction => correction.NewBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(correction => correction.Reason)
            .HasMaxLength(AccountMovementCorrection.MaximumReasonLength)
            .IsRequired();

        builder.Property(correction => correction.CorrelationId)
            .HasMaxLength(AccountMovementCorrection.MaximumCorrelationIdLength)
            .IsRequired();

        builder.Property(correction => correction.CorrectedAtUtc)
            .HasPrecision(7)
            .IsRequired();

        builder.HasOne<AccountMovement>()
            .WithMany()
            .HasForeignKey(correction => correction.MovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(correction => new
        {
            correction.MovementId,
            correction.CorrectedAtUtc,
        })
            .HasDatabaseName("IX_AccountMovementCorrections_MovementId_CorrectedAtUtc");

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AccountMovementCorrections_PreviousType",
                "[PreviousType] IN ('Deposit', 'Withdrawal')");
            tableBuilder.HasCheckConstraint(
                "CK_AccountMovementCorrections_PreviousValue",
                "([PreviousType] = 'Deposit' AND [PreviousValue] > 0) OR " +
                "([PreviousType] = 'Withdrawal' AND [PreviousValue] < 0)");
            tableBuilder.HasCheckConstraint(
                "CK_AccountMovementCorrections_PreviousBalance",
                "[PreviousBalance] >= 0");
            tableBuilder.HasCheckConstraint(
                "CK_AccountMovementCorrections_NewType",
                "[NewType] IN ('Deposit', 'Withdrawal')");
            tableBuilder.HasCheckConstraint(
                "CK_AccountMovementCorrections_NewValue",
                "([NewType] = 'Deposit' AND [NewValue] > 0) OR " +
                "([NewType] = 'Withdrawal' AND [NewValue] < 0)");
            tableBuilder.HasCheckConstraint(
                "CK_AccountMovementCorrections_NewBalance",
                "[NewBalance] >= 0");
        });
    }
}
