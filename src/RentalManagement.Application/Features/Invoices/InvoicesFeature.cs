using FluentValidation;
using MediatR;
using RentalManagement.Application.Common;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Domain.Interfaces;
using Unit = RentalManagement.Domain.Entities.Unit;

namespace RentalManagement.Application.Features.Invoices;

// ─── DTOs ────────────────────────────────────────────────────

public record InvoiceDto(
    Guid Id,
    string Number,
    Guid ContractId,
    string ContractNumber,
    string TenantName,
    string UnitNumber,
    string PropertyName,
    string Type,
    decimal Amount,
    decimal PaidAmount,
    decimal DebtAmount,
    DateTime DueDate,
    string Status,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    string? Description,
    DateTime CreatedAt,
    IEnumerable<PaymentDto> Payments);

public record PaymentDto(
    Guid Id,
    Guid InvoiceId,
    decimal Amount,
    DateTime PaymentDate,
    string? PaymentMethod,
    string? Reference,
    string? Notes);

public record AddPaymentDto(
    decimal Amount,
    DateTime PaymentDate,
    string? PaymentMethod,
    string? Reference,
    string? Notes);

public record CreateInvoiceDto(
    Guid ContractId,
    InvoiceType Type,
    decimal Amount,
    DateTime DueDate,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    string? Description);

// ─── QUERIES ─────────────────────────────────────────────────

public record GetInvoicesQuery(
    int Page = 1,
    int PageSize = 20,
    PaymentStatus? Status = null,
    Guid? ContractId = null,
    Guid? TenantId = null,
    string? Month = null) // "2024-11"
    : IRequest<PagedResult<InvoiceDto>>;

public record GetInvoiceByIdQuery(Guid Id)
    : IRequest<Result<InvoiceDto>>;

public record GetOverdueInvoicesQuery()
    : IRequest<IEnumerable<InvoiceDto>>;

// ─── COMMANDS ────────────────────────────────────────────────

public record CreateInvoiceCommand(CreateInvoiceDto Data)
    : IRequest<Result<InvoiceDto>>;

public record AddPaymentCommand(Guid InvoiceId, AddPaymentDto Data)
    : IRequest<Result<InvoiceDto>>;

public record GenerateMonthlyRentInvoicesCommand(string Month) // "2024-11"
    : IRequest<Result<int>>; // returns count of generated invoices

public record CancelInvoiceCommand(Guid Id, string Reason)
    : IRequest<Result>;

// ─── VALIDATORS ──────────────────────────────────────────────

public class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.Data.ContractId).NotEmpty();
        RuleFor(x => x.Data.Amount).GreaterThan(0);
        RuleFor(x => x.Data.DueDate).NotEmpty();
    }
}

public class AddPaymentValidator : AbstractValidator<AddPaymentCommand>
{
    public AddPaymentValidator()
    {
        RuleFor(x => x.Data.Amount).GreaterThan(0);
        RuleFor(x => x.Data.PaymentDate).NotEmpty();
    }
}

// ─── SERVICE INTERFACE ───────────────────────────────────────

public interface IInvoiceNumberService
{
    Task<string> GenerateNumberAsync(CancellationToken ct = default);
}

// ─── HANDLERS ────────────────────────────────────────────────

public class GetInvoicesHandler : IRequestHandler<GetInvoicesQuery, PagedResult<InvoiceDto>>
{
    private readonly IRepository<Invoice> _invoices;
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<Tenant> _tenants;
    private readonly IRepository<Unit> _units;
    private readonly IRepository<Property> _props;
    private readonly IRepository<Payment> _payments;

    public GetInvoicesHandler(
        IRepository<Invoice> invoices, IRepository<Contract> contracts,
        IRepository<Tenant> tenants, IRepository<Unit> units,
        IRepository<Property> props, IRepository<Payment> payments)
    { _invoices = invoices; _contracts = contracts; _tenants = tenants; _units = units; _props = props; _payments = payments; }

    public async Task<PagedResult<InvoiceDto>> Handle(GetInvoicesQuery request, CancellationToken ct)
    {
        var allInvoices = await _invoices.GetAllAsync(ct);
        var allContracts = await _contracts.GetAllAsync(ct);
        var allTenants = await _tenants.GetAllAsync(ct);
        var allUnits = await _units.GetAllAsync(ct);
        var allProps = await _props.GetAllAsync(ct);
        var allPayments = await _payments.GetAllAsync(ct);

        var contractsDict = allContracts.ToDictionary(c => c.Id);
        var tenantsDict = allTenants.ToDictionary(t => t.Id);
        var unitsDict = allUnits.ToDictionary(u => u.Id);
        var propsDict = allProps.ToDictionary(p => p.Id);
        var paymentsByInvoice = allPayments.GroupBy(p => p.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());

        var filtered = allInvoices.Where(i => !i.IsDeleted);

        if (request.Status.HasValue)
            filtered = filtered.Where(i => i.Status == request.Status.Value);

        if (request.ContractId.HasValue)
            filtered = filtered.Where(i => i.ContractId == request.ContractId.Value);

        if (request.TenantId.HasValue)
        {
            var tenantContracts = allContracts.Where(c => c.TenantId == request.TenantId.Value).Select(c => c.Id).ToHashSet();
            filtered = filtered.Where(i => tenantContracts.Contains(i.ContractId));
        }

        if (!string.IsNullOrWhiteSpace(request.Month) && DateTime.TryParse(request.Month + "-01", out var monthDate))
        {
            var endOfMonth = monthDate.AddMonths(1).AddDays(-1);
            filtered = filtered.Where(i => i.DueDate >= monthDate && i.DueDate <= endOfMonth);
        }

        // Auto-mark overdue
        var now = DateTime.UtcNow;
        foreach (var inv in filtered.Where(i => i.Status == PaymentStatus.Pending && i.DueDate < now))
        {
            inv.Status = PaymentStatus.Overdue;
        }

        var total = filtered.Count();
        var items = filtered
            .OrderByDescending(i => i.DueDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => {
                contractsDict.TryGetValue(i.ContractId, out var c);
                Tenant? tenant = null;
                Unit? unit = null;
                Property? prop = null;
                if (c != null)
                {
                    tenantsDict.TryGetValue(c.TenantId, out tenant);
                    unitsDict.TryGetValue(c.UnitId, out unit);
                    if (unit != null) propsDict.TryGetValue(unit.PropertyId, out prop);
                }
                var payments = paymentsByInvoice.GetValueOrDefault(i.Id, new List<Payment>())
                    .Select(p => new PaymentDto(p.Id, p.InvoiceId, p.Amount, p.PaymentDate, p.PaymentMethod, p.Reference, p.Notes));
                return new InvoiceDto(
                    i.Id, i.Number, i.ContractId, c?.Number ?? "", tenant?.FullName ?? "",
                    unit?.Number ?? "", prop?.Name ?? "",
                    i.Type.ToString(), i.Amount, i.PaidAmount, i.DebtAmount,
                    i.DueDate, i.Status.ToString(), i.PeriodStart, i.PeriodEnd, i.Description,
                    i.CreatedAt, payments);
            })
            .ToList();

        return new PagedResult<InvoiceDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }
}

public class AddPaymentHandler : IRequestHandler<AddPaymentCommand, Result<InvoiceDto>>
{
    private readonly IRepository<Invoice> _invoices;
    private readonly IRepository<Payment> _payments;
    private readonly IUnitOfWork _uow;

    public AddPaymentHandler(IRepository<Invoice> invoices, IRepository<Payment> payments, IUnitOfWork uow)
    { _invoices = invoices; _payments = payments; _uow = uow; }

    public async Task<Result<InvoiceDto>> Handle(AddPaymentCommand cmd, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(cmd.InvoiceId, ct);
        if (invoice == null || invoice.IsDeleted)
            return Result<InvoiceDto>.Failure("Рахунок не знайдено");
        if (invoice.Status == PaymentStatus.Cancelled)
            return Result<InvoiceDto>.Failure("Неможливо додати оплату до скасованого рахунку");
        if (invoice.Status == PaymentStatus.Paid)
            return Result<InvoiceDto>.Failure("Рахунок вже оплачено");

        var d = cmd.Data;
        if (d.Amount > invoice.DebtAmount)
            return Result<InvoiceDto>.Failure($"Сума оплати ({d.Amount}) перевищує залишок боргу ({invoice.DebtAmount})");

        var payment = new Payment
        {
            InvoiceId = cmd.InvoiceId,
            Amount = d.Amount,
            PaymentDate = d.PaymentDate,
            PaymentMethod = d.PaymentMethod,
            Reference = d.Reference,
            Notes = d.Notes
        };
        await _payments.AddAsync(payment, ct);

        invoice.PaidAmount += d.Amount;
        invoice.Status = invoice.PaidAmount >= invoice.Amount
            ? PaymentStatus.Paid
            : PaymentStatus.PartiallyPaid;
        invoice.UpdatedAt = DateTime.UtcNow;
        _invoices.Update(invoice);

        await _uow.SaveChangesAsync(ct);

        return Result<InvoiceDto>.Success(new InvoiceDto(
            invoice.Id, invoice.Number, invoice.ContractId, "", "", "", "",
            invoice.Type.ToString(), invoice.Amount, invoice.PaidAmount, invoice.DebtAmount,
            invoice.DueDate, invoice.Status.ToString(), invoice.PeriodStart, invoice.PeriodEnd,
            invoice.Description, invoice.CreatedAt,
            new[] { new PaymentDto(payment.Id, payment.InvoiceId, payment.Amount, payment.PaymentDate, payment.PaymentMethod, payment.Reference, payment.Notes) }));
    }
}

public class GenerateMonthlyRentInvoicesHandler : IRequestHandler<GenerateMonthlyRentInvoicesCommand, Result<int>>
{
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<Invoice> _invoices;
    private readonly IUnitOfWork _uow;
    private readonly IInvoiceNumberService _numberService;

    public GenerateMonthlyRentInvoicesHandler(
        IRepository<Contract> contracts, IRepository<Invoice> invoices,
        IUnitOfWork uow, IInvoiceNumberService numberService)
    { _contracts = contracts; _invoices = invoices; _uow = uow; _numberService = numberService; }

    public async Task<Result<int>> Handle(GenerateMonthlyRentInvoicesCommand cmd, CancellationToken ct)
    {
        if (!DateTime.TryParse(cmd.Month + "-01", out var monthDate))
            return Result<int>.Failure("Невірний формат місяця. Очікується YYYY-MM");

        var periodStart = monthDate;
        var periodEnd = monthDate.AddMonths(1).AddDays(-1);

        var activeContracts = await _contracts.FindAsync(
            c => !c.IsDeleted && c.Status == ContractStatus.Active &&
                 c.StartDate <= periodEnd && c.EndDate >= periodStart, ct);

        var existingInvoices = await _invoices.FindAsync(
            i => !i.IsDeleted && i.Type == InvoiceType.Rent &&
                 i.PeriodStart == periodStart && i.PeriodEnd == periodEnd, ct);

        var alreadyGeneratedContracts = existingInvoices.Select(i => i.ContractId).ToHashSet();
        var toGenerate = activeContracts.Where(c => !alreadyGeneratedContracts.Contains(c.Id)).ToList();

        int count = 0;
        foreach (var contract in toGenerate)
        {
            var dueDate = new DateTime(monthDate.Year, monthDate.Month, contract.PaymentDayOfMonth);
            if (dueDate < periodStart) dueDate = dueDate.AddMonths(1);

            var invoice = new Invoice
            {
                Number = await _numberService.GenerateNumberAsync(ct),
                ContractId = contract.Id,
                Type = InvoiceType.Rent,
                Amount = contract.MonthlyRent,
                DueDate = dueDate,
                Status = PaymentStatus.Pending,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Description = $"Орендна плата за {monthDate:MMMM yyyy}"
            };
            await _invoices.AddAsync(invoice, ct);
            count++;
        }

        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(count);
    }
}

public class CancelInvoiceHandler : IRequestHandler<CancelInvoiceCommand, Result>
{
    private readonly IRepository<Invoice> _invoices;
    private readonly IUnitOfWork _uow;

    public CancelInvoiceHandler(IRepository<Invoice> invoices, IUnitOfWork uow) { _invoices = invoices; _uow = uow; }

    public async Task<Result> Handle(CancelInvoiceCommand cmd, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(cmd.Id, ct);
        if (invoice == null) return Result.Failure("Рахунок не знайдено");
        if (invoice.Status == PaymentStatus.Paid)
            return Result.Failure("Неможливо скасувати оплачений рахунок");

        invoice.Status = PaymentStatus.Cancelled;
        invoice.Description = $"[СКАСОВАНО: {cmd.Reason}] {invoice.Description}";
        invoice.UpdatedAt = DateTime.UtcNow;
        _invoices.Update(invoice);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
