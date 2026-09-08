using Devsu.Customers.Application.Contracts;
using Devsu.Customers.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Devsu.Customers.Api.Controllers;

[ApiController]
[Route("api/clientes")]
[Produces("application/json")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpPost]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Create(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        CustomerResponse customer = await _customerService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { clienteId = customer.CustomerId },
            customer);
    }

    [HttpGet("{clienteId:guid}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(
        [FromRoute(Name = "clienteId")] Guid customerId,
        CancellationToken cancellationToken)
    {
        CustomerResponse customer = await _customerService.GetByIdAsync(customerId, cancellationToken);
        return Ok(customer);
    }

    [HttpGet]
    [ProducesResponseType<PageResponse<CustomerResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageResponse<CustomerResponse>>> List(
        [FromQuery(Name = "pagina")] int page = 1,
        [FromQuery(Name = "tamanoPagina")] int pageSize = 20,
        [FromQuery(Name = "estado")] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        PageResponse<CustomerResponse> response = await _customerService.ListAsync(
            page,
            pageSize,
            isActive,
            cancellationToken);

        return Ok(response);
    }

    [HttpPut("{clienteId:guid}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Update(
        [FromRoute(Name = "clienteId")] Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        CustomerResponse customer = await _customerService.UpdateAsync(
            customerId,
            request,
            cancellationToken);

        return Ok(customer);
    }

    [HttpPatch("{clienteId:guid}/estado")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> ChangeStatus(
        [FromRoute(Name = "clienteId")] Guid customerId,
        ChangeCustomerStatusRequest request,
        CancellationToken cancellationToken)
    {
        CustomerResponse customer = await _customerService.ChangeStatusAsync(
            customerId,
            request,
            cancellationToken);

        return Ok(customer);
    }

    [HttpDelete("{clienteId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute(Name = "clienteId")] Guid customerId,
        CancellationToken cancellationToken)
    {
        await _customerService.DeleteAsync(customerId, cancellationToken);
        return NoContent();
    }
}
