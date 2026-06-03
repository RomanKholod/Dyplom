using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalManagement.Application.Features.Reports;

namespace RentalManagement.API.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExportController(IMediator mediator) => _mediator = mediator;

    // ─── EXCEL ────────────────────────────────────────────────

    /// <summary>Експорт рахунків у Excel</summary>
    [HttpGet("excel/invoices")]
    public async Task<IActionResult> ExportInvoicesExcel(
        [FromQuery] string? month = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var bytes = await _mediator.Send(new ExportInvoicesExcelQuery(month, status), ct);
        var filename = $"invoices_{month ?? DateTime.Now.ToString("yyyy-MM")}_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    /// <summary>Експорт договорів у Excel</summary>
    [HttpGet("excel/contracts")]
    public async Task<IActionResult> ExportContractsExcel(
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var bytes = await _mediator.Send(new ExportContractsExcelQuery(status), ct);
        var filename = $"contracts_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    /// <summary>Експорт орендарів у Excel</summary>
    [HttpGet("excel/tenants")]
    public async Task<IActionResult> ExportTenantsExcel(CancellationToken ct = default)
    {
        var bytes = await _mediator.Send(new ExportTenantsExcelQuery(), ct);
        var filename = $"tenants_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    /// <summary>Звіт по боргах у Excel</summary>
    [HttpGet("excel/debt-report")]
    public async Task<IActionResult> ExportDebtReportExcel(CancellationToken ct = default)
    {
        var bytes = await _mediator.Send(new ExportDebtReportExcelQuery(), ct);
        var filename = $"debt_report_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    // ─── PDF ──────────────────────────────────────────────────

    /// <summary>Рахунок у PDF</summary>
    [HttpGet("pdf/invoice/{id:guid}")]
    public async Task<IActionResult> ExportInvoicePdf(Guid id, CancellationToken ct = default)
    {
        try
        {
            var bytes = await _mediator.Send(new ExportInvoicePdfQuery(id), ct);
            return File(bytes, "application/pdf", $"invoice_{id}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Договір у PDF</summary>
    [HttpGet("pdf/contract/{id:guid}")]
    public async Task<IActionResult> ExportContractPdf(Guid id, CancellationToken ct = default)
    {
        try
        {
            var bytes = await _mediator.Send(new ExportContractPdfQuery(id), ct);
            return File(bytes, "application/pdf", $"contract_{id}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Звіт по боргах у PDF</summary>
    [HttpGet("pdf/debt-report")]
    public async Task<IActionResult> ExportDebtReportPdf(CancellationToken ct = default)
    {
        var bytes = await _mediator.Send(new ExportDebtReportPdfQuery(), ct);
        return File(bytes, "application/pdf", $"debt_report_{DateTime.Now:yyyyMMdd}.pdf");
    }
}
