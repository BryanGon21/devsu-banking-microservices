using Devsu.Accounts.Application.Contracts;
using Devsu.Accounts.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Devsu.Accounts.Api.Controllers;

[ApiController]
[Route("api/reportes")]
[Produces("application/json")]
public sealed class AccountStatementsController : ControllerBase
{
    private readonly IAccountStatementService _statementService;

    public AccountStatementsController(IAccountStatementService statementService)
    {
        _statementService = statementService;
    }

    [HttpGet]
    [ProducesResponseType<AccountStatementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountStatementResponse>> Get(
        [FromQuery(Name = "fechaInicio")] DateOnly? startDate,
        [FromQuery(Name = "fechaFin")] DateOnly? endDate,
        [FromQuery(Name = "clienteId")] Guid? customerId,
        CancellationToken cancellationToken)
    {
        AccountStatementResponse statement = await _statementService.GetAsync(
            startDate,
            endDate,
            customerId,
            cancellationToken);

        return Ok(statement);
    }
}
