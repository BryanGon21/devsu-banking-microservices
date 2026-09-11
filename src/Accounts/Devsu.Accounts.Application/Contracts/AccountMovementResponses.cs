using System.Text.Json.Serialization;
using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.Application.Contracts;

public sealed record AccountMovementResponse(
    [property: JsonPropertyName("movimientoId")] long Id,
    [property: JsonPropertyName("numeroCuenta")] string AccountNumber,
    [property: JsonPropertyName("fecha")] DateTimeOffset OccurredAtUtc,
    [property: JsonPropertyName("tipoMovimiento")] MovementType Type,
    [property: JsonPropertyName("valor")] decimal Value,
    [property: JsonPropertyName("saldoDisponible")] decimal Balance,
    [property: JsonPropertyName("creadoEnUtc")] DateTimeOffset CreatedAtUtc,
    [property: JsonPropertyName("actualizadoEnUtc")] DateTimeOffset UpdatedAtUtc);

public sealed record CreateAccountMovementResponse(
    AccountMovementResponse Movement,
    bool WasCreated);
