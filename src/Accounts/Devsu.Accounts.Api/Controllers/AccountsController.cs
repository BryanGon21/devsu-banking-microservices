using Devsu.Accounts.Application.Contracts;
using Devsu.Accounts.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Devsu.Accounts.Api.Controllers;

[ApiController]
[Route("api/cuentas")]
[Produces("application/json")]
public sealed class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountResponse>> Create(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        AccountResponse account = await _accountService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetByNumber),
            new { numeroCuenta = account.Number },
            account);
    }

    [HttpGet("{numeroCuenta}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> GetByNumber(
        [FromRoute(Name = "numeroCuenta")] string number,
        CancellationToken cancellationToken)
    {
        AccountResponse account = await _accountService.GetByNumberAsync(number, cancellationToken);
        return Ok(account);
    }

    [HttpGet]
    [ProducesResponseType<PageResponse<AccountResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageResponse<AccountResponse>>> List(
        [FromQuery(Name = "pagina")] int page = 1,
        [FromQuery(Name = "tamanoPagina")] int pageSize = 20,
        [FromQuery(Name = "clienteId")] Guid? customerId = null,
        [FromQuery(Name = "estado")] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        PageResponse<AccountResponse> response = await _accountService.ListAsync(
            page,
            pageSize,
            customerId,
            isActive,
            cancellationToken);

        return Ok(response);
    }

    [HttpPut("{numeroCuenta}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountResponse>> Update(
        [FromRoute(Name = "numeroCuenta")] string number,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        AccountResponse account = await _accountService.UpdateAsync(
            number,
            request,
            cancellationToken);

        return Ok(account);
    }

    [HttpPatch("{numeroCuenta}/estado")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountResponse>> ChangeStatus(
        [FromRoute(Name = "numeroCuenta")] string number,
        ChangeAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        AccountResponse account = await _accountService.ChangeStatusAsync(
            number,
            request,
            cancellationToken);

        return Ok(account);
    }
}
