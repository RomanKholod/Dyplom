using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalManagement.Application.Features.Properties;
using RentalManagement.Domain.Enums;

namespace RentalManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PropertiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PropertiesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] PropertyStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPropertiesQuery(page, pageSize, search, status), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPropertyByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreatePropertyDto dto, CancellationToken ct)
    {
        var cmd = new CreatePropertyCommand(
            dto.Name, dto.Address, dto.City, dto.Type,
            dto.TotalArea, dto.FloorsCount, dto.OwnerId, dto.Description);

        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeletePropertyCommand(id), ct);
        return result.IsSuccess ? NoContent() : NotFound(new { error = result.Error });
    }

    [HttpGet("{id:guid}/units")]
    public async Task<IActionResult> GetUnits(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUnitsByPropertyQuery(id), ct);
        return Ok(result);
    }

    [HttpPost("units")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateUnit([FromBody] CreateUnitDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateUnitCommand(dto), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
