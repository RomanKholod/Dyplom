using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentalManagement.Application.Features.Notifications;
using RentalManagement.Domain.Enums;
using RentalManagement.Infrastructure.Persistence;

namespace RentalManagement.Infrastructure.BackgroundJobs;

/// <summary>
/// Щоденна перевірка прострочених рахунків — надсилає email-нагадування орендарям.
/// Запускається Hangfire через Program.cs: RecurringJob.AddOrUpdate(...)
/// </summary>
public class OverdueInvoiceNotificationJob
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<OverdueInvoiceNotificationJob> _logger;

    public OverdueInvoiceNotificationJob(
        AppDbContext db,
        IEmailService email,
        ILogger<OverdueInvoiceNotificationJob> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Running OverdueInvoiceNotificationJob at {Time}", DateTime.UtcNow);

        var now = DateTime.UtcNow;

        // Знаходимо прострочені рахунки з активними договорами
        var overdueInvoices = await _db.Invoices
            .Include(i => i.Contract)
                .ThenInclude(c => c.Tenant)
            .Where(i =>
                !i.IsDeleted &&
                i.Status != PaymentStatus.Paid &&
                i.Status != PaymentStatus.Cancelled &&
                i.DueDate < now &&
                i.DebtAmount > 0)
            .ToListAsync();

        // Оновлюємо статус на Overdue якщо ще Pending
        foreach (var inv in overdueInvoices.Where(i => i.Status == PaymentStatus.Pending))
        {
            inv.Status = PaymentStatus.Overdue;
            inv.UpdatedAt = now;
        }
        await _db.SaveChangesAsync();

        // Надсилаємо сповіщення (уникаємо дублів — тільки ті що просрочені на 1, 7, 14, 30 днів)
        var notifyDays = new[] { 1, 7, 14, 30 };
        int sent = 0;

        foreach (var inv in overdueInvoices)
        {
            var tenant = inv.Contract?.Tenant;
            if (tenant == null || string.IsNullOrEmpty(tenant.Email)) continue;

            var overdueDays = (int)(now - inv.DueDate).TotalDays;
            if (!notifyDays.Contains(overdueDays)) continue;

            try
            {
                await _email.SendOverdueNotificationAsync(
                    tenant.Email, tenant.FullName,
                    inv.Number, inv.DebtAmount, inv.DueDate);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send overdue notification for invoice {InvoiceId}", inv.Id);
            }
        }

        _logger.LogInformation("OverdueInvoiceNotificationJob done. Notified: {Count}", sent);
    }
}

/// <summary>
/// Щоденна перевірка договорів, що закінчуються — надсилає попередження.
/// </summary>
public class ExpiringContractNotificationJob
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<ExpiringContractNotificationJob> _logger;

    public ExpiringContractNotificationJob(
        AppDbContext db,
        IEmailService email,
        ILogger<ExpiringContractNotificationJob> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Running ExpiringContractNotificationJob at {Time}", DateTime.UtcNow);

        var now = DateTime.UtcNow.Date;
        var notifyThresholds = new[] { 30, 14, 7, 3, 1 };

        var contracts = await _db.Contracts
            .Include(c => c.Tenant)
            .Include(c => c.Unit).ThenInclude(u => u.Property)
            .Where(c =>
                !c.IsDeleted &&
                c.Status == ContractStatus.Active &&
                c.EndDate >= now &&
                c.EndDate <= now.AddDays(30))
            .ToListAsync();

        int sent = 0;
        foreach (var contract in contracts)
        {
            var tenant = contract.Tenant;
            if (tenant == null || string.IsNullOrEmpty(tenant.Email)) continue;

            var daysLeft = (int)(contract.EndDate.Date - now).TotalDays;
            if (!notifyThresholds.Contains(daysLeft)) continue;

            try
            {
                await _email.SendContractExpiringNotificationAsync(
                    tenant.Email, tenant.FullName,
                    contract.Number, contract.EndDate, daysLeft);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send expiring notification for contract {ContractId}", contract.Id);
            }
        }

        _logger.LogInformation("ExpiringContractNotificationJob done. Notified: {Count}", sent);
    }
}

/// <summary>
/// Щомісячна автоматична генерація орендних рахунків.
/// Запускається 1-го числа кожного місяця о 08:00.
/// </summary>
public class MonthlyInvoiceGenerationJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<MonthlyInvoiceGenerationJob> _logger;

    public MonthlyInvoiceGenerationJob(AppDbContext db, ILogger<MonthlyInvoiceGenerationJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        _logger.LogInformation("Running MonthlyInvoiceGenerationJob for {Month}", month);

        var monthDate  = DateTime.Parse(month + "-01");
        var periodEnd  = monthDate.AddMonths(1).AddDays(-1);
        var now        = DateTime.UtcNow;

        var activeContracts = await _db.Contracts
            .Where(c => !c.IsDeleted &&
                        c.Status == ContractStatus.Active &&
                        c.StartDate <= periodEnd &&
                        c.EndDate >= monthDate)
            .ToListAsync();

        var existing = await _db.Invoices
            .Where(i => !i.IsDeleted &&
                        i.Type == InvoiceType.Rent &&
                        i.PeriodStart == monthDate &&
                        i.PeriodEnd == periodEnd)
            .Select(i => i.ContractId)
            .ToListAsync();

        var toGenerate = activeContracts.Where(c => !existing.Contains(c.Id)).ToList();

        int count = 0;
        foreach (var contract in toGenerate)
        {
            var year  = monthDate.Year;
            var mon   = monthDate.Month;
            var seq   = await _db.Invoices.CountAsync(i => i.CreatedAt.Year == year && i.CreatedAt.Month == mon) + count + 1;

            var dueDate = new DateTime(year, mon, contract.PaymentDayOfMonth);
            if (dueDate < monthDate) dueDate = dueDate.AddMonths(1);

            _db.Invoices.Add(new Domain.Entities.Invoice
            {
                Number      = $"РХ-{year}{mon:D2}-{seq:D4}",
                ContractId  = contract.Id,
                Type        = InvoiceType.Rent,
                Amount      = contract.MonthlyRent,
                DueDate     = dueDate,
                Status      = PaymentStatus.Pending,
                PeriodStart = monthDate,
                PeriodEnd   = periodEnd,
                Description = $"Орендна плата за {monthDate:MMMM yyyy}",
            });
            count++;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("MonthlyInvoiceGenerationJob done. Generated: {Count}", count);
    }
}
