using System.Text.Json.Serialization;
using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.Application.Contracts;

public sealed record CreateAccountRequest(
    [property: JsonRequired, JsonPropertyName("numeroCuenta")] string Number,
    [property: JsonRequired, JsonPropertyName("tipoCuenta")] AccountType Type,
    [property: JsonRequired, JsonPropertyName("saldoInicial")] decimal InitialBalance,
    [property: JsonRequired, JsonPropertyName("estado")] bool IsActive,
    [property: JsonRequired, JsonPropertyName("clienteId")] Guid CustomerId);

public sealed record UpdateAccountRequest(
    [property: JsonRequired, JsonPropertyName("tipoCuenta")] AccountType Type,
    [property: JsonRequired, JsonPropertyName("saldoInicial")] decimal InitialBalance,
    [property: JsonRequired, JsonPropertyName("estado")] bool IsActive);

public sealed record ChangeAccountStatusRequest(
    [property: JsonRequired, JsonPropertyName("estado")] bool IsActive);
