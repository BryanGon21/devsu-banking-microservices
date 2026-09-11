using Devsu.Customers.Domain.Enums;
using Devsu.Customers.Domain.Exceptions;

namespace Devsu.Customers.Domain.Entities;

public abstract class Person
{
    public const int MaximumNameLength = 100;
    public const int MaximumIdentificationLength = 20;
    public const int MaximumAddressLength = 200;
    public const int MaximumPhoneLength = 25;
    public const int MaximumAge = 130;

    protected Person()
    {
    }

    protected Person(
        Guid id,
        string name,
        Gender gender,
        int age,
        string identification,
        string address,
        string phone)
    {
        if (id == Guid.Empty)
        {
            throw new BusinessRuleException("customer_invalid_id", "The customer identifier is invalid.");
        }

        Id = id;
        Name = ValidateRequiredText(name, nameof(name), MaximumNameLength);
        Gender = ValidateGender(gender);
        Age = ValidateAge(age);
        Identification = ValidateRequiredText(
            identification,
            nameof(identification),
            MaximumIdentificationLength);
        Address = ValidateRequiredText(address, nameof(address), MaximumAddressLength);
        Phone = ValidateRequiredText(phone, nameof(phone), MaximumPhoneLength);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Gender Gender { get; private set; }

    public int Age { get; private set; }

    public string Identification { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    protected bool UpdatePersonalDetails(
        string name,
        Gender gender,
        int age,
        string identification,
        string address,
        string phone)
    {
        string validatedName = ValidateRequiredText(name, nameof(name), MaximumNameLength);
        Gender validatedGender = ValidateGender(gender);
        int validatedAge = ValidateAge(age);
        string validatedIdentification = ValidateRequiredText(
            identification,
            nameof(identification),
            MaximumIdentificationLength);
        string validatedAddress = ValidateRequiredText(address, nameof(address), MaximumAddressLength);
        string validatedPhone = ValidateRequiredText(phone, nameof(phone), MaximumPhoneLength);

        bool changed =
            !string.Equals(Name, validatedName, StringComparison.Ordinal) ||
            Gender != validatedGender ||
            Age != validatedAge ||
            !string.Equals(Identification, validatedIdentification, StringComparison.Ordinal) ||
            !string.Equals(Address, validatedAddress, StringComparison.Ordinal) ||
            !string.Equals(Phone, validatedPhone, StringComparison.Ordinal);

        if (!changed)
        {
            return false;
        }

        Name = validatedName;
        Gender = validatedGender;
        Age = validatedAge;
        Identification = validatedIdentification;
        Address = validatedAddress;
        Phone = validatedPhone;

        return true;
    }

    private static string ValidateRequiredText(string value, string field, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException("customer_required_field", $"The {field} field is required.");
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new BusinessRuleException(
                "customer_invalid_field",
                $"The {field} field cannot exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }

    private static int ValidateAge(int age)
    {
        if (age is < 0 or > MaximumAge)
        {
            throw new BusinessRuleException(
                "customer_invalid_age",
                $"Age must be between 0 and {MaximumAge}.");
        }

        return age;
    }

    private static Gender ValidateGender(Gender gender)
    {
        if (gender == Gender.Unspecified || !Enum.IsDefined(gender))
        {
            throw new BusinessRuleException("customer_invalid_gender", "The specified gender is invalid.");
        }

        return gender;
    }
}
