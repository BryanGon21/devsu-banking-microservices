using System.Text.Json.Serialization;
using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.Application.Contracts;

public sealed record AccountStatementResponse(
    [property: JsonPropertyName("cliente")] AccountStatementCustomerResponse Customer,
    [property: JsonPropertyName("fechaInicio")] DateOnly StartDate,
    [property: JsonPropertyName("fechaFin")] DateOnly EndDate,
    [property: JsonPropertyName("cuentas")] IReadOnlyCollection<AccountStatementAccountResponse> Accounts);

public sealed record AccountStatementCustomerResponse(
    [property: JsonPropertyName("clienteId")] Guid CustomerId,
    [property: JsonPropertyName("nombre")] string Name);

public sealed record AccountStatementAccountResponse(
    [property: JsonPropertyName("numeroCuenta")] string Number,
    [property: JsonPropertyName("tipoCuenta")] AccountType Type,
    [property: JsonPropertyName("saldoInicial")] decimal InitialBalance,
    [property: JsonPropertyName("saldoInicioPeriodo")] decimal OpeningBalance,
    [property: JsonPropertyName("saldoFinPeriodo")] decimal ClosingBalance,
    [property: JsonPropertyName("saldoActual")] decimal CurrentBalance,
    [property: JsonPropertyName("estado")] bool IsActive,
    [property: JsonPropertyName("movimientos")] IReadOnlyCollection<AccountStatementMovementResponse> Movements);

public sealed record AccountStatementMovementResponse(
    [property: JsonPropertyName("movimientoId")] long Id,
    [property: JsonPropertyName("fecha")] DateTimeOffset OccurredAtUtc,
    [property: JsonPropertyName("tipoMovimiento")] MovementType Type,
    [property: JsonPropertyName("valor")] decimal Value,
    [property: JsonPropertyName("saldoDisponible")] decimal Balance);
