using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RentalManagement.Application.Features.Reports;
using RentalManagement.Domain.Enums;
using RentalManagement.Infrastructure.Persistence;

namespace RentalManagement.Infrastructure.Services.Export;

public class PdfExportService : IPdfExportService
{
    private readonly AppDbContext _db;

    public PdfExportService(AppDbContext db)
    {
        _db = db;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ─── INVOICE PDF ─────────────────────────────────────────

    public async Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Contract).ThenInclude(c => c.Tenant)
            .Include(i => i.Contract).ThenInclude(c => c.Unit).ThenInclude(u => u.Property)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException("Рахунок не знайдено");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Rental Management System")
                                .FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                            c.Item().Text("Система управління орендою нерухомості")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                        row.ConstantItem(120).AlignRight().Column(c =>
                        {
                            c.Item().Text($"РАХУНОК").FontSize(16).Bold();
                            c.Item().Text(invoice.Number).FontSize(12).FontColor(Colors.Blue.Medium);
                        });
                    });
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Invoice info grid
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("ОРЕНДАР").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                            c.Item().PaddingTop(3).Text(invoice.Contract?.Tenant?.FullName ?? "").Bold();
                            c.Item().Text($"Email: {invoice.Contract?.Tenant?.Email ?? ""}");
                            c.Item().Text($"Тел: {invoice.Contract?.Tenant?.Phone ?? ""}");
                            if (!string.IsNullOrEmpty(invoice.Contract?.Tenant?.TaxCode))
                                c.Item().Text($"ІПН: {invoice.Contract.Tenant.TaxCode}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("ОБ'ЄКТ").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                            c.Item().PaddingTop(3).Text(invoice.Contract?.Unit?.Property?.Name ?? "").Bold();
                            c.Item().Text($"Приміщення № {invoice.Contract?.Unit?.Number ?? ""}");
                            c.Item().Text(invoice.Contract?.Unit?.Property?.Address ?? "");
                        });
                        row.ConstantItem(150).Column(c =>
                        {
                            c.Item().Text("РАХУНОК").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                            c.Item().PaddingTop(3).Text($"№ {invoice.Number}").Bold();
                            c.Item().Text($"Дата: {invoice.CreatedAt:dd.MM.yyyy}");
                            c.Item().Text($"Строк: {invoice.DueDate:dd.MM.yyyy}");
                            if (invoice.PeriodStart.HasValue)
                                c.Item().Text($"Період: {invoice.PeriodStart:MM.yyyy}");
                        });
                    });

                    col.Item().PaddingVertical(15);

                    // Table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(30);
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        // Header
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Blue.Darken2).Padding(6).Text("№").FontColor(Colors.White).Bold();
                            h.Cell().Background(Colors.Blue.Darken2).Padding(6).Text("Опис").FontColor(Colors.White).Bold();
                            h.Cell().Background(Colors.Blue.Darken2).Padding(6).Text("К-сть").FontColor(Colors.White).Bold().AlignCenter();
                            h.Cell().Background(Colors.Blue.Darken2).Padding(6).Text("Сума (грн)").FontColor(Colors.White).Bold().AlignRight();
                        });

                        // Row
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text("1");
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(
                            invoice.Description ?? $"{invoice.Type} — {invoice.PeriodStart:MMMM yyyy}");
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignCenter().Text("1");
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight()
                            .Text($"{invoice.Amount:#,##0.00}");
                    });

                    col.Item().PaddingVertical(10);

                    // Totals
                    col.Item().AlignRight().Width(200).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(100); });

                        void SummaryRow(string label, decimal value, bool bold = false, string? color = null)
                        {
                            var labelCell = table.Cell().Padding(4).Text(label);
                            var valueCell = table.Cell().Padding(4).AlignRight().Text($"{value:#,##0.00} грн");
                            if (bold) { labelCell.Bold(); valueCell.Bold(); }
                            if (color != null) { labelCell.FontColor(color); valueCell.FontColor(color); }
                        }

                        SummaryRow("Нараховано:", invoice.Amount);
                        SummaryRow("Оплачено:", invoice.PaidAmount, color: Colors.Green.Darken1);
                        table.Cell().ColumnSpan(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        SummaryRow("До сплати:", invoice.DebtAmount, bold: true,
                            color: invoice.DebtAmount > 0 ? Colors.Red.Medium : Colors.Green.Darken1);
                    });

                    // Payments history
                    if (invoice.Payments.Any())
                    {
                        col.Item().PaddingTop(20).Text("Історія оплат").Bold();
                        col.Item().PaddingTop(5).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(30); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
                            });
                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("№").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Дата").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Сума").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Метод").Bold();
                            });

                            int i = 1;
                            foreach (var p in invoice.Payments.OrderBy(p => p.PaymentDate))
                            {
                                table.Cell().Padding(4).Text(i++.ToString());
                                table.Cell().Padding(4).Text(p.PaymentDate.ToString("dd.MM.yyyy"));
                                table.Cell().Padding(4).Text($"{p.Amount:#,##0.00} грн");
                                table.Cell().Padding(4).Text(p.PaymentMethod ?? "—");
                            }
                        });
                    }

                    // Status badge
                    col.Item().PaddingTop(15);
                    var statusColor = invoice.Status switch
                    {
                        PaymentStatus.Paid => Colors.Green.Darken1,
                        PaymentStatus.Overdue => Colors.Red.Medium,
                        PaymentStatus.PartiallyPaid => Colors.Orange.Medium,
                        _ => Colors.Grey.Medium
                    };
                    var statusText = invoice.Status switch
                    {
                        PaymentStatus.Paid => "ОПЛАЧЕНО",
                        PaymentStatus.Overdue => "ПРОСТРОЧЕНО",
                        PaymentStatus.PartiallyPaid => "ЧАСТКОВО ОПЛАЧЕНО",
                        _ => "ОЧІКУЄ ОПЛАТИ"
                    };
                    col.Item().AlignLeft().Background(statusColor).Padding(6).Width(160)
                        .Text(statusText).FontColor(Colors.White).Bold();
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Згенеровано: {DateTime.Now:dd.MM.yyyy HH:mm}  |  ").FontSize(8).FontColor(Colors.Grey.Medium);
                    t.Span("Rental Management System").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return doc.GeneratePdf();
    }

    // ─── CONTRACT PDF ─────────────────────────────────────────

    public async Task<byte[]> GenerateContractPdfAsync(Guid contractId, CancellationToken ct = default)
    {
        var contract = await _db.Contracts
            .Include(c => c.Tenant)
            .Include(c => c.Unit).ThenInclude(u => u.Property).ThenInclude(p => p.Owner)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new InvalidOperationException("Договір не знайдено");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // Title
                    col.Item().AlignCenter().Text("ДОГОВІР ОРЕНДИ").FontSize(16).Bold();
                    col.Item().AlignCenter().Text($"№ {contract.Number}").FontSize(13).Bold()
                        .FontColor(Colors.Blue.Medium);
                    col.Item().AlignCenter().PaddingBottom(5)
                        .Text($"від {contract.CreatedAt:dd.MM.yyyy}").FontSize(10).FontColor(Colors.Grey.Medium);
                    col.Item().LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                    col.Item().PaddingVertical(10);

                    // Parties
                    col.Item().Text("1. СТОРОНИ ДОГОВОРУ").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text(text =>
                    {
                        text.Span("Орендодавець: ").Bold();
                        text.Span($"{contract.Unit?.Property?.Owner?.FullName ?? "—"}, ");
                        if (!string.IsNullOrEmpty(contract.Unit?.Property?.Owner?.TaxCode))
                            text.Span($"ІПН: {contract.Unit.Property.Owner.TaxCode}");
                    });
                    col.Item().PaddingTop(4).Text(text =>
                    {
                        text.Span("Орендар: ").Bold();
                        text.Span($"{contract.Tenant?.FullName ?? "—"}, ");
                        text.Span($"тел: {contract.Tenant?.Phone ?? "—"}, ");
                        text.Span($"email: {contract.Tenant?.Email ?? "—"}");
                        if (!string.IsNullOrEmpty(contract.Tenant?.TaxCode))
                            text.Span($", ІПН: {contract.Tenant.TaxCode}");
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                    // Subject
                    col.Item().PaddingTop(5).Text("2. ПРЕДМЕТ ДОГОВОРУ").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text(text =>
                    {
                        text.Span("Об'єкт оренди: ").Bold();
                        text.Span($"{contract.Unit?.Property?.Name ?? "—"}, приміщення № {contract.Unit?.Number ?? "—"}, ");
                        text.Span($"поверх {contract.Unit?.Floor ?? 0}, площа {contract.Unit?.Area ?? 0} м²");
                    });
                    col.Item().PaddingTop(4).Text(text =>
                    {
                        text.Span("Адреса: ").Bold();
                        text.Span($"{contract.Unit?.Property?.Address ?? "—"}, {contract.Unit?.Property?.City ?? "—"}");
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                    // Terms
                    col.Item().PaddingTop(5).Text("3. УМОВИ ОРЕНДИ").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

                        void Row(string label, string value)
                        {
                            table.Cell().Padding(5).Background(Colors.Grey.Lighten4).Text(label).Bold();
                            table.Cell().Padding(5).Text(value);
                        }

                        Row("Дата початку:", contract.StartDate.ToString("dd.MM.yyyy"));
                        Row("Дата закінчення:", contract.EndDate.ToString("dd.MM.yyyy"));
                        Row("Орендна плата:", $"{contract.MonthlyRent:#,##0.00} грн/місяць");
                        Row("Заставна сума:", $"{contract.SecurityDeposit:#,##0.00} грн");
                        Row("День оплати:", $"{contract.PaymentDayOfMonth}-го числа кожного місяця");
                        Row("Статус:", contract.Status.ToString());
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                    // Notes
                    if (!string.IsNullOrEmpty(contract.Notes))
                    {
                        col.Item().PaddingTop(5).Text("4. ПРИМІТКИ").Bold().FontSize(12);
                        col.Item().PaddingTop(8).Text(contract.Notes);
                        col.Item().PaddingVertical(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    }

                    // Signatures
                    col.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("ОРЕНДОДАВЕЦЬ:").Bold();
                            c.Item().PaddingTop(5).Text(contract.Unit?.Property?.Owner?.FullName ?? "—");
                            c.Item().PaddingTop(30).Text("Підпис: ___________________");
                            c.Item().PaddingTop(5).Text("Дата: _____________________");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("ОРЕНДАР:").Bold();
                            c.Item().PaddingTop(5).Text(contract.Tenant?.FullName ?? "—");
                            c.Item().PaddingTop(30).Text("Підпис: ___________________");
                            c.Item().PaddingTop(5).Text("Дата: _____________________");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Rental Management System  |  {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return doc.GeneratePdf();
    }

    // ─── DEBT REPORT PDF ─────────────────────────────────────

    public async Task<byte[]> GenerateDebtReportPdfAsync(CancellationToken ct = default)
    {
        var invoices = await _db.Invoices
            .Include(i => i.Contract).ThenInclude(c => c.Tenant)
            .Include(i => i.Contract).ThenInclude(c => c.Unit).ThenInclude(u => u.Property)
            .Where(i => !i.IsDeleted && i.Status != PaymentStatus.Paid && i.Status != PaymentStatus.Cancelled && i.DebtAmount > 0)
            .OrderByDescending(i => i.DueDate)
            .ToListAsync(ct);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("ЗВІТ ПО ЗАБОРГОВАНОСТЯХ").FontSize(14).Bold();
                        row.ConstantItem(200).AlignRight().Text($"Станом на: {DateTime.Now:dd.MM.yyyy}").FontSize(10);
                    });
                    col.Item().PaddingTop(3).LineHorizontal(2).LineColor(Colors.Red.Medium);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    // Summary
                    var totalDebt = invoices.Sum(i => i.DebtAmount);
                    col.Item().Background(Colors.Red.Lighten4).Padding(8).Row(row =>
                    {
                        row.RelativeItem().Text($"Загальний борг:").Bold();
                        row.ConstantItem(150).AlignRight().Text($"{totalDebt:#,##0.00} грн").Bold().FontColor(Colors.Red.Darken2);
                        row.ConstantItem(100).AlignRight().Text($"Рахунків: {invoices.Count}").Bold();
                    });

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(25); c.RelativeColumn(2); c.RelativeColumn(2);
                            c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
                        });

                        table.Header(h =>
                        {
                            foreach (var hdr in new[] { "№", "Орендар", "Об'єкт", "Рахунок", "Нараховано", "Оплачено", "Борг", "Строк / Днів" })
                                h.Cell().Background(Colors.Red.Darken1).Padding(5).Text(hdr).FontColor(Colors.White).Bold();
                        });

                        var now = DateTime.UtcNow;
                        int i = 1;
                        foreach (var inv in invoices)
                        {
                            var overdue = (int)(now - inv.DueDate).TotalDays;
                            var bg = overdue > 30 ? Colors.Red.Lighten4 : overdue > 0 ? Colors.Yellow.Lighten4 : Colors.White;

                            table.Cell().Background(bg).Padding(4).Text(i++.ToString());
                            table.Cell().Background(bg).Padding(4).Text(inv.Contract?.Tenant?.FullName ?? "");
                            table.Cell().Background(bg).Padding(4).Text(inv.Contract?.Unit?.Property?.Name ?? "");
                            table.Cell().Background(bg).Padding(4).Text(inv.Number);
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{inv.Amount:#,##0.00}");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{inv.PaidAmount:#,##0.00}");
                            table.Cell().Background(bg).Padding(4).AlignRight().Text($"{inv.DebtAmount:#,##0.00}").Bold().FontColor(Colors.Red.Medium);
                            table.Cell().Background(bg).Padding(4).AlignCenter()
                                .Text($"{inv.DueDate:dd.MM.yy}\n{(overdue > 0 ? $"+{overdue} дн." : "—")}");
                        }
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}
