using System.Text.Json;
using System.Text.Json.Serialization;
using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.Api.Serialization;

internal sealed class SpanishMovementTypeJsonConverter : JsonConverter<MovementType>
{
    public override MovementType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Movement type must be a string value.");
        }

        return reader.GetString()?.Trim().ToUpperInvariant() switch
        {
            "DEPOSITO" => MovementType.Deposit,
            "RETIRO" => MovementType.Withdrawal,
            _ => throw new JsonException("Movement type must be 'Deposito' or 'Retiro'."),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        MovementType value,
        JsonSerializerOptions options)
    {
        string externalValue = value switch
        {
            MovementType.Deposit => "Deposito",
            MovementType.Withdrawal => "Retiro",
            _ => throw new JsonException("The movement type value is invalid."),
        };

        writer.WriteStringValue(externalValue);
    }
}
