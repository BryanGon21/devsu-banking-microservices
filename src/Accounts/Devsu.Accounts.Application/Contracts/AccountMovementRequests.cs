using System.Text.Json.Serialization;
using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.Application.Contracts;

public sealed record CreateAccountMovementRequest(
    [property: JsonRequired, JsonPropertyName("numeroCuenta")] string AccountNumber,
    [property: JsonRequired, JsonPropertyName("tipoMovimiento")] MovementType Type,
    [property: JsonRequired, JsonPropertyName("valor")] decimal Value);

public sealed record CorrectAccountMovementRequest(
    [property: JsonRequired, JsonPropertyName("fecha")] DateTimeOffset OccurredAtUtc,
    [property: JsonRequired, JsonPropertyName("tipoMovimiento")] MovementType Type,
    [property: JsonRequired, JsonPropertyName("valor")] decimal Value,
    [property: JsonRequired, JsonPropertyName("motivo")] string Reason);
