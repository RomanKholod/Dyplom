using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalManagement.Application.Features.Contracts;
using RentalManagement.Domain.Enums;

namespace RentalManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContractsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ContractsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] ContractStatus? status = null, [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? propertyId = null, CancellationToken ct = default)
    {
        // Removed the invalid 'search' text argument to match GetContractsQuery parameters exactly
        var query = new GetContractsQuery(page, pageSize, status, tenantId, propertyId, ExpiringWithin30Days: false);
        return Ok(await _mediator.Send(query, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetContractByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiring([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        // Routed expiring endpoint to use the existing GetContractsQuery filter flag
        var query = new GetContractsQuery(Page: page, PageSize: pageSize, ExpiringWithin30Days: true);
        return Ok(await _mediator.Send(query, ct));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateContractDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateContractCommand(dto), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ActivateContractCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:guid}/terminate")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Terminate(Guid id, [FromBody] TerminateContractDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new TerminateContractCommand(id, dto.Reason), ct);
        // Fixed: plain Result has no .Value, we return a success status message or object instead
        return result.IsSuccess ? Ok(new { message = "Contract successfully terminated" }) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:guid}/renew")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Renew(Guid id, [FromBody] RenewContractRequest dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new RenewContractCommand(id, dto.NewEndDate, dto.NewMonthlyRent), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}

public record RenewContractRequest(DateTime NewEndDate, decimal? NewMonthlyRent);
public record TerminateContractDto(string Reason);