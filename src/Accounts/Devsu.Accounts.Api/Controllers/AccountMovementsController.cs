using Devsu.Accounts.Application.Contracts;
using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Devsu.Accounts.Api.Controllers;

[ApiController]
[Route("api/movimientos")]
[Produces("application/json")]
public sealed class AccountMovementsController : ControllerBase
{
    private const string IdempotencyHeaderName = "Idempotency-Key";

    private readonly IAccountMovementService _movementService;

    public AccountMovementsController(IAccountMovementService movementService)
    {
        _movementService = movementService;
    }

    [HttpPost]
    [ProducesResponseType<AccountMovementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AccountMovementResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AccountMovementResponse>> Create(
        CreateAccountMovementRequest request,
        [FromHeader(Name = IdempotencyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        StringValues headerValues = Request.Headers[IdempotencyHeaderName];
        if (headerValues.Count > 1)
        {
            throw new ValidationException(
                "idempotency_key_multiple_values",
                "Idempotency-Key must contain exactly one value.");
        }

        CreateAccountMovementResponse result = await _movementService.CreateAsync(
            request,
            idempotencyKey,
            cancellationToken);

        if (!result.WasCreated)
        {
            return Ok(result.Movement);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { movimientoId = result.Movement.Id },
            result.Movement);
    }

    [HttpGet("{movimientoId:long}")]
    [ProducesResponseType<AccountMovementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountMovementResponse>> GetById(
        [FromRoute(Name = "movimientoId")] long movementId,
        CancellationToken cancellationToken)
    {
        AccountMovementResponse movement = await _movementService.GetByIdAsync(
            movementId,
            cancellationToken);

        return Ok(movement);
    }

    [HttpPut("{movimientoId:long}")]
    [ProducesResponseType<AccountMovementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AccountMovementResponse>> Correct(
        [FromRoute(Name = "movimientoId")] long movementId,
        CorrectAccountMovementRequest request,
        CancellationToken cancellationToken)
    {
        AccountMovementResponse movement = await _movementService.CorrectAsync(
            movementId,
            request,
            HttpContext.TraceIdentifier,
            cancellationToken);

        return Ok(movement);
    }

    [HttpGet]
    [ProducesResponseType<PageResponse<AccountMovementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageResponse<AccountMovementResponse>>> List(
        [FromQuery(Name = "pagina")] int page = 1,
        [FromQuery(Name = "tamanoPagina")] int pageSize = 20,
        [FromQuery(Name = "numeroCuenta")] string? accountNumber = null,
        [FromQuery(Name = "fechaInicio")] DateOnly? startDate = null,
        [FromQuery(Name = "fechaFin")] DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        PageResponse<AccountMovementResponse> response = await _movementService.ListAsync(
            page,
            pageSize,
            accountNumber,
            startDate,
            endDate,
            cancellationToken);

        return Ok(response);
    }
}
