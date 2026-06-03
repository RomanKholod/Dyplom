using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalManagement.Application.Features.Meters;

namespace RentalManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetersController : ControllerBase
{
    private readonly IMediator _mediator;
    public MetersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMetersQuery(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMeterByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpGet("{id:guid}/readings")]
    public async Task<IActionResult> GetReadings(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetMeterReadingsQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateMeterDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateMeterCommand(dto), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:guid}/readings")]
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public async Task<IActionResult> AddReading(Guid id, [FromBody] AddReadingDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddReadingCommand(id, dto), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteMeterCommand(id), ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }
}
