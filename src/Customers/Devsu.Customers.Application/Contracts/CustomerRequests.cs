using System.Text.Json.Serialization;
using Devsu.Customers.Domain.Enums;

namespace Devsu.Customers.Application.Contracts;

public sealed record CreateCustomerRequest(
    [property: JsonPropertyName("nombre")] string Name,
    [property: JsonPropertyName("genero")] Gender Gender,
    [property: JsonPropertyName("edad")] int Age,
    [property: JsonPropertyName("identificacion")] string Identification,
    [property: JsonPropertyName("direccion")] string Address,
    [property: JsonPropertyName("telefono")] string Phone,
    [property: JsonPropertyName("contrasena")] string Password,
    [property: JsonPropertyName("estado")] bool IsActive = true);

public sealed record UpdateCustomerRequest(
    [property: JsonPropertyName("nombre")] string Name,
    [property: JsonPropertyName("genero")] Gender Gender,
    [property: JsonPropertyName("edad")] int Age,
    [property: JsonPropertyName("identificacion")] string Identification,
    [property: JsonPropertyName("direccion")] string Address,
    [property: JsonPropertyName("telefono")] string Phone,
    [property: JsonPropertyName("contrasena")] string? Password,
    [property: JsonPropertyName("estado")] bool IsActive);

public sealed record ChangeCustomerStatusRequest(
    [property: JsonPropertyName("estado")] bool IsActive);
