using System.Text.Json;
using System.Text.Json.Serialization;
using Devsu.Customers.Domain.Enums;

namespace Devsu.Customers.Api.Serialization;

internal sealed class SpanishGenderJsonConverter : JsonConverter<Gender>
{
    public override Gender Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Gender must be a string value.");
        }

        return reader.GetString()?.Trim().ToUpperInvariant() switch
        {
            "MASCULINO" => Gender.Male,
            "FEMENINO" => Gender.Female,
            "OTRO" => Gender.Other,
            _ => throw new JsonException("Gender must be Masculino, Femenino, or Otro."),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        Gender value,
        JsonSerializerOptions options)
    {
        if (value == Gender.Unspecified)
        {
            writer.WriteNullValue();
            return;
        }

        string externalValue = value switch
        {
            Gender.Male => "Masculino",
            Gender.Female => "Femenino",
            Gender.Other => "Otro",
            _ => throw new JsonException("The internal gender value cannot be serialized."),
        };

        writer.WriteStringValue(externalValue);
    }
}
