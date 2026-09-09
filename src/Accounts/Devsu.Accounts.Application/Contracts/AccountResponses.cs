using System.Text.Json.Serialization;
using Devsu.Accounts.Domain.Enums;

namespace Devsu.Accounts.Application.Contracts;

public sealed record AccountResponse(
    [property: JsonPropertyName("numeroCuenta")] string Number,
    [property: JsonPropertyName("tipoCuenta")] AccountType Type,
    [property: JsonPropertyName("saldoInicial")] decimal InitialBalance,
    [property: JsonPropertyName("saldoActual")] decimal CurrentBalance,
    [property: JsonPropertyName("estado")] bool IsActive,
    [property: JsonPropertyName("clienteId")] Guid CustomerId,
    [property: JsonPropertyName("creadoEnUtc")] DateTimeOffset CreatedAtUtc,
    [property: JsonPropertyName("actualizadoEnUtc")] DateTimeOffset UpdatedAtUtc);

public sealed record PageResponse<T>(
    [property: JsonPropertyName("items")] IReadOnlyCollection<T> Items,
    [property: JsonPropertyName("pagina")] int Page,
    [property: JsonPropertyName("tamanoPagina")] int PageSize,
    [property: JsonPropertyName("totalItems")] int TotalItems,
    [property: JsonPropertyName("totalPaginas")] int TotalPages);
