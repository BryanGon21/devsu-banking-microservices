using System.Text.Json.Serialization;
using Devsu.Customers.Domain.Enums;

namespace Devsu.Customers.Application.Contracts;

public sealed record CustomerResponse(
    [property: JsonPropertyName("clienteId")] Guid CustomerId,
    [property: JsonPropertyName("nombre")] string Name,
    [property: JsonPropertyName("genero")] Gender Gender,
    [property: JsonPropertyName("edad")] int Age,
    [property: JsonPropertyName("identificacion")] string Identification,
    [property: JsonPropertyName("direccion")] string Address,
    [property: JsonPropertyName("telefono")] string Phone,
    [property: JsonPropertyName("estado")] bool IsActive,
    [property: JsonPropertyName("version")] long Version,
    [property: JsonPropertyName("creadoEnUtc")] DateTimeOffset CreatedAtUtc,
    [property: JsonPropertyName("actualizadoEnUtc")] DateTimeOffset UpdatedAtUtc);

public sealed record PageResponse<T>(
    [property: JsonPropertyName("items")] IReadOnlyCollection<T> Items,
    [property: JsonPropertyName("pagina")] int Page,
    [property: JsonPropertyName("tamanoPagina")] int PageSize,
    [property: JsonPropertyName("totalItems")] int TotalItems,
    [property: JsonPropertyName("totalPaginas")] int TotalPages);
