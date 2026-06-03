using MediatR;

namespace RentalManagement.Application.Features.Reports;

// ─── INTERFACES ───────────────────────────────────────────────

public interface IExcelExportService
{
    Task<byte[]> ExportInvoicesAsync(string? month = null, string? status = null, CancellationToken ct = default);
    Task<byte[]> ExportContractsAsync(string? status = null, CancellationToken ct = default);
    Task<byte[]> ExportTenantsAsync(CancellationToken ct = default);
    Task<byte[]> ExportDebtReportAsync(CancellationToken ct = default);
}

public interface IPdfExportService
{
    Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId, CancellationToken ct = default);
    Task<byte[]> GenerateContractPdfAsync(Guid contractId, CancellationToken ct = default);
    Task<byte[]> GenerateDebtReportPdfAsync(CancellationToken ct = default);
}

// ─── QUERIES ─────────────────────────────────────────────────

public record ExportInvoicesExcelQuery(string? Month = null, string? Status = null)
    : IRequest<byte[]>;

public record ExportContractsExcelQuery(string? Status = null)
    : IRequest<byte[]>;

public record ExportTenantsExcelQuery : IRequest<byte[]>;

public record ExportDebtReportExcelQuery : IRequest<byte[]>;

public record ExportInvoicePdfQuery(Guid InvoiceId) : IRequest<byte[]>;
public record ExportContractPdfQuery(Guid ContractId) : IRequest<byte[]>;
public record ExportDebtReportPdfQuery : IRequest<byte[]>;

// ─── HANDLERS ────────────────────────────────────────────────

public class ExportInvoicesExcelHandler : IRequestHandler<ExportInvoicesExcelQuery, byte[]>
{
    private readonly IExcelExportService _excel;
    public ExportInvoicesExcelHandler(IExcelExportService excel) => _excel = excel;
    public Task<byte[]> Handle(ExportInvoicesExcelQuery q, CancellationToken ct)
        => _excel.ExportInvoicesAsync(q.Month, q.Status, ct);
}

public class ExportContractsExcelHandler : IRequestHandler<ExportContractsExcelQuery, byte[]>
{
    private readonly IExcelExportService _excel;
    public ExportContractsExcelHandler(IExcelExportService excel) => _excel = excel;
    public Task<byte[]> Handle(ExportContractsExcelQuery q, CancellationToken ct)
        => _excel.ExportContractsAsync(q.Status, ct);
}

public class ExportTenantsExcelHandler : IRequestHandler<ExportTenantsExcelQuery, byte[]>
{
    private readonly IExcelExportService _excel;
    public ExportTenantsExcelHandler(IExcelExportService excel) => _excel = excel;
    public Task<byte[]> Handle(ExportTenantsExcelQuery q, CancellationToken ct)
        => _excel.ExportTenantsAsync(ct);
}

public class ExportDebtReportExcelHandler : IRequestHandler<ExportDebtReportExcelQuery, byte[]>
{
    private readonly IExcelExportService _excel;
    public ExportDebtReportExcelHandler(IExcelExportService excel) => _excel = excel;
    public Task<byte[]> Handle(ExportDebtReportExcelQuery q, CancellationToken ct)
        => _excel.ExportDebtReportAsync(ct);
}

public class ExportInvoicePdfHandler : IRequestHandler<ExportInvoicePdfQuery, byte[]>
{
    private readonly IPdfExportService _pdf;
    public ExportInvoicePdfHandler(IPdfExportService pdf) => _pdf = pdf;
    public Task<byte[]> Handle(ExportInvoicePdfQuery q, CancellationToken ct)
        => _pdf.GenerateInvoicePdfAsync(q.InvoiceId, ct);
}

public class ExportContractPdfHandler : IRequestHandler<ExportContractPdfQuery, byte[]>
{
    private readonly IPdfExportService _pdf;
    public ExportContractPdfHandler(IPdfExportService pdf) => _pdf = pdf;
    public Task<byte[]> Handle(ExportContractPdfQuery q, CancellationToken ct)
        => _pdf.GenerateContractPdfAsync(q.ContractId, ct);
}

public class ExportDebtReportPdfHandler : IRequestHandler<ExportDebtReportPdfQuery, byte[]>
{
    private readonly IPdfExportService _pdf;
    public ExportDebtReportPdfHandler(IPdfExportService pdf) => _pdf = pdf;
    public Task<byte[]> Handle(ExportDebtReportPdfQuery q, CancellationToken ct)
        => _pdf.GenerateDebtReportPdfAsync(ct);
}
