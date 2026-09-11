using Devsu.Customers.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devsu.Customers.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People");
        builder.HasKey(person => person.Id);

        builder.Property(person => person.Id)
            .ValueGeneratedNever();

        builder.Property(person => person.Name)
            .HasMaxLength(Person.MaximumNameLength)
            .IsRequired();

        builder.Property(person => person.Gender)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(person => person.Age)
            .IsRequired();

        builder.Property(person => person.Identification)
            .HasMaxLength(Person.MaximumIdentificationLength)
            .IsRequired();

        builder.Property(person => person.Address)
            .HasMaxLength(Person.MaximumAddressLength)
            .IsRequired();

        builder.Property(person => person.Phone)
            .HasMaxLength(Person.MaximumPhoneLength)
            .IsRequired();

        builder.HasIndex(person => person.Identification)
            .IsUnique()
            .HasDatabaseName("UX_People_Identification");

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_People_Age",
                $"[Age] >= 0 AND [Age] <= {Person.MaximumAge}");
            tableBuilder.HasCheckConstraint(
                "CK_People_Gender",
                "[Gender] IN ('Male', 'Female', 'Other')");
        });
    }
}
