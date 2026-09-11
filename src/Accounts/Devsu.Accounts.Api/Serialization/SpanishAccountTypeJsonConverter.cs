using System.Text.Json;
using System.Text.Json.Serialization;
using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.Api.Serialization;

internal sealed class SpanishAccountTypeJsonConverter : JsonConverter<AccountType>
{
    public override AccountType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Account type must be a string value.");
        }

        return reader.GetString()?.Trim().ToUpperInvariant() switch
        {
            "AHORROS" => AccountType.Savings,
            "CORRIENTE" => AccountType.Checking,
            _ => throw new JsonException("Account type must be 'Ahorros' or 'Corriente'."),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        AccountType value,
        JsonSerializerOptions options)
    {
        string externalValue = value switch
        {
            AccountType.Savings => "Ahorros",
            AccountType.Checking => "Corriente",
            _ => throw new JsonException("The account type value is invalid."),
        };

        writer.WriteStringValue(externalValue);
    }
}
