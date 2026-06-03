using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalManagement.Application.Features.Invoices;
using RentalManagement.Domain.Enums;

namespace RentalManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    public InvoicesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] Guid? contractId = null, [FromQuery] Guid? tenantId = null,
        [FromQuery] string? month = null, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetInvoicesQuery(page, pageSize, status, contractId, tenantId, month), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInvoiceByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue(CancellationToken ct)
        => Ok(await _mediator.Send(new GetOverdueInvoicesQuery(), ct));

    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public async Task<IActionResult> AddPayment(Guid id, [FromBody] AddPaymentDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddPaymentCommand(id, dto), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("generate-rent")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> GenerateRent([FromQuery] string month, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(month))
            month = DateTime.UtcNow.ToString("yyyy-MM");
        var result = await _mediator.Send(new GenerateMonthlyRentInvoicesCommand(month), ct);
        return result.IsSuccess
            ? Ok(new { generated = result.Value, month })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelInvoiceRequest dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelInvoiceCommand(id, dto.Reason), ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }
}

public record CancelInvoiceRequest(string Reason);
