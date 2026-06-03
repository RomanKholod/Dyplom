using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RentalManagement.Application.Features.Reports;
using RentalManagement.Domain.Enums;
using RentalManagement.Infrastructure.Persistence;

namespace RentalManagement.Infrastructure.Services.Export;

public class ExcelExportService : IExcelExportService
{
    private readonly AppDbContext _db;

    public ExcelExportService(AppDbContext db) => _db = db;

    // ─── INVOICES ─────────────────────────────────────────────

    public async Task<byte[]> ExportInvoicesAsync(string? month = null, string? status = null, CancellationToken ct = default)
    {
        var query = _db.Invoices
            .Include(i => i.Contract)
                .ThenInclude(c => c.Tenant)
            .Include(i => i.Contract)
                .ThenInclude(c => c.Unit)
                    .ThenInclude(u => u.Property)
            .Where(i => !i.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month + "-01", out var mDate))
        {
            var mEnd = mDate.AddMonths(1);
            query = query.Where(i => i.DueDate >= mDate && i.DueDate < mEnd);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PaymentStatus>(status, out var ps))
            query = query.Where(i => i.Status == ps);

        var invoices = await query.OrderByDescending(i => i.DueDate).ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Рахунки");

        // Header style
        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
        headerRow.Style.Font.FontColor = XLColor.White;

        // Headers
        string[] headers = ["№", "Номер рахунку", "Договір", "Орендар", "Об'єкт", "Приміщення",
            "Тип", "Сума (грн)", "Оплачено (грн)", "Борг (грн)", "Строк оплати", "Статус", "Період"];

        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        // Data rows
        for (int row = 0; row < invoices.Count; row++)
        {
            var inv = invoices[row];
            var r   = row + 2;

            ws.Cell(r, 1).Value  = row + 1;
            ws.Cell(r, 2).Value  = inv.Number;
            ws.Cell(r, 3).Value  = inv.Contract?.Number ?? "";
            ws.Cell(r, 4).Value  = inv.Contract?.Tenant?.FullName ?? "";
            ws.Cell(r, 5).Value  = inv.Contract?.Unit?.Property?.Name ?? "";
            ws.Cell(r, 6).Value  = inv.Contract?.Unit?.Number ?? "";
            ws.Cell(r, 7).Value  = inv.Type.ToString();
            ws.Cell(r, 8).Value  = inv.Amount;
            ws.Cell(r, 9).Value  = inv.PaidAmount;
            ws.Cell(r, 10).Value = inv.DebtAmount;
            ws.Cell(r, 11).Value = inv.DueDate.ToString("dd.MM.yyyy");
            ws.Cell(r, 12).Value = inv.Status.ToString();
            ws.Cell(r, 13).Value = inv.PeriodStart.HasValue
                ? $"{inv.PeriodStart:dd.MM.yyyy} – {inv.PeriodEnd:dd.MM.yyyy}"
                : "";

            // Color overdue
            if (inv.Status == PaymentStatus.Overdue)
                ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF2F2");
            else if (inv.Status == PaymentStatus.Paid)
                ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0FDF4");

            // Number format
            ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 10).Style.NumberFormat.Format = "#,##0.00";
        }

        // Totals row
        var totalRow = invoices.Count + 2;
        ws.Cell(totalRow, 7).Value = "РАЗОМ:";
        ws.Cell(totalRow, 7).Style.Font.Bold = true;
        ws.Cell(totalRow, 8).Value = invoices.Sum(i => i.Amount);
        ws.Cell(totalRow, 9).Value = invoices.Sum(i => i.PaidAmount);
        ws.Cell(totalRow, 10).Value = invoices.Sum(i => i.DebtAmount);
        ws.Row(totalRow).Style.Font.Bold = true;
        ws.Row(totalRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 5;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── CONTRACTS ────────────────────────────────────────────

    public async Task<byte[]> ExportContractsAsync(string? status = null, CancellationToken ct = default)
    {
        var query = _db.Contracts
            .Include(c => c.Tenant)
            .Include(c => c.Unit).ThenInclude(u => u.Property)
            .Where(c => !c.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContractStatus>(status, out var cs))
            query = query.Where(c => c.Status == cs);

        var contracts = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Договори");

        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
        ws.Row(1).Style.Font.FontColor = XLColor.White;

        string[] headers = ["№", "Номер договору", "Орендар", "ІПН/Код", "Об'єкт",
            "Приміщення", "Площа (м²)", "Початок", "Закінчення", "Орендна плата (грн)",
            "Застава (грн)", "День оплати", "Статус", "Залишилось днів"];

        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var now = DateTime.UtcNow;
        for (int row = 0; row < contracts.Count; row++)
        {
            var c = contracts[row];
            var r = row + 2;
            var days = (int)(c.EndDate - now).TotalDays;

            ws.Cell(r, 1).Value  = row + 1;
            ws.Cell(r, 2).Value  = c.Number;
            ws.Cell(r, 3).Value  = c.Tenant?.FullName ?? "";
            ws.Cell(r, 4).Value  = c.Tenant?.TaxCode ?? "";
            ws.Cell(r, 5).Value  = c.Unit?.Property?.Name ?? "";
            ws.Cell(r, 6).Value  = c.Unit?.Number ?? "";
            ws.Cell(r, 7).Value  = c.Unit?.Area ?? 0;
            ws.Cell(r, 8).Value  = c.StartDate.ToString("dd.MM.yyyy");
            ws.Cell(r, 9).Value  = c.EndDate.ToString("dd.MM.yyyy");
            ws.Cell(r, 10).Value = c.MonthlyRent;
            ws.Cell(r, 11).Value = c.SecurityDeposit;
            ws.Cell(r, 12).Value = c.PaymentDayOfMonth;
            ws.Cell(r, 13).Value = c.Status.ToString();
            ws.Cell(r, 14).Value = days;

            ws.Cell(r, 10).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 11).Style.NumberFormat.Format = "#,##0.00";

            if (c.Status == ContractStatus.Active && days <= 30 && days > 0)
                ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFFBEB");
            else if (c.Status == ContractStatus.Terminated)
                ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF2F2");
            else if (c.Status == ContractStatus.Active)
                ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0FDF4");
        }

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 5;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── TENANTS ─────────────────────────────────────────────

    public async Task<byte[]> ExportTenantsAsync(CancellationToken ct = default)
    {
        var tenants = await _db.Tenants
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.LastName)
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Орендарі");

        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
        ws.Row(1).Style.Font.FontColor = XLColor.White;

        string[] headers = ["№", "ПІБ / Назва", "Тип", "Email", "Телефон", "ІПН / ЄДРПОУ", "Паспорт", "Нотатки"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        for (int row = 0; row < tenants.Count; row++)
        {
            var t = tenants[row];
            var r = row + 2;
            ws.Cell(r, 1).Value = row + 1;
            ws.Cell(r, 2).Value = t.FullName;
            ws.Cell(r, 3).Value = t.IsCompany ? "Юр. особа" : "Фіз. особа";
            ws.Cell(r, 4).Value = t.Email;
            ws.Cell(r, 5).Value = t.Phone;
            ws.Cell(r, 6).Value = t.TaxCode ?? "";
            ws.Cell(r, 7).Value = t.PassportNumber ?? "";
            ws.Cell(r, 8).Value = t.Notes ?? "";
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── DEBT REPORT ─────────────────────────────────────────

    public async Task<byte[]> ExportDebtReportAsync(CancellationToken ct = default)
    {
        var invoices = await _db.Invoices
            .Include(i => i.Contract).ThenInclude(c => c.Tenant)
            .Include(i => i.Contract).ThenInclude(c => c.Unit).ThenInclude(u => u.Property)
            .Where(i => !i.IsDeleted &&
                        i.Status != PaymentStatus.Paid &&
                        i.Status != PaymentStatus.Cancelled &&
                        i.DebtAmount > 0)
            .OrderByDescending(i => i.DueDate)
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Борги");

        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DC2626");
        ws.Row(1).Style.Font.FontColor = XLColor.White;

        string[] headers = ["№", "Орендар", "Об'єкт", "Номер рахунку", "Сума (грн)",
            "Оплачено (грн)", "Борг (грн)", "Строк оплати", "Прострочено (днів)", "Статус"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var now = DateTime.UtcNow;
        for (int row = 0; row < invoices.Count; row++)
        {
            var inv = invoices[row];
            var r   = row + 2;
            var overdueDays = inv.DueDate < now ? (int)(now - inv.DueDate).TotalDays : 0;

            ws.Cell(r, 1).Value  = row + 1;
            ws.Cell(r, 2).Value  = inv.Contract?.Tenant?.FullName ?? "";
            ws.Cell(r, 3).Value  = inv.Contract?.Unit?.Property?.Name ?? "";
            ws.Cell(r, 4).Value  = inv.Number;
            ws.Cell(r, 5).Value  = inv.Amount;
            ws.Cell(r, 6).Value  = inv.PaidAmount;
            ws.Cell(r, 7).Value  = inv.DebtAmount;
            ws.Cell(r, 8).Value  = inv.DueDate.ToString("dd.MM.yyyy");
            ws.Cell(r, 9).Value  = overdueDays;
            ws.Cell(r, 10).Value = inv.Status.ToString();

            ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 7).Style.NumberFormat.Format = "#,##0.00";

            if (overdueDays > 30)
                ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
            else if (overdueDays > 0)
                ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9C3");
        }

        // Summary
        var total = invoices.Count + 2;
        ws.Cell(total, 6).Value = "РАЗОМ БОРГІВ:";
        ws.Cell(total, 6).Style.Font.Bold = true;
        ws.Cell(total, 7).Value = invoices.Sum(i => i.DebtAmount);
        ws.Cell(total, 7).Style.Font.Bold = true;
        ws.Cell(total, 7).Style.NumberFormat.Format = "#,##0.00";
        ws.Row(total).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF1F2");

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
