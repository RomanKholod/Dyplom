using MediatR;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Domain.Interfaces;
using Unit = RentalManagement.Domain.Entities.Unit;

namespace RentalManagement.Application.Features.Dashboard;

public record DashboardStatsDto(
    int TotalProperties,
    int TotalUnits,
    int OccupiedUnits,
    double OccupancyRate,
    int ActiveContracts,
    int ExpiringContracts,     // expiring in 30 days
    decimal MonthlyRevenue,    // current month invoiced
    decimal TotalDebt,
    int OverdueInvoices,
    IEnumerable<MonthlyRevenueDto> RevenueChart);

public record MonthlyRevenueDto(string Month, decimal Invoiced, decimal Paid);

public record GetDashboardStatsQuery : IRequest<DashboardStatsDto>;

public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IRepository<Property> _props;
    private readonly IRepository<Unit> _units;
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<Invoice> _invoices;

    public GetDashboardStatsHandler(
        IRepository<Property> props, IRepository<Unit> units,
        IRepository<Contract> contracts, IRepository<Invoice> invoices)
    { _props = props; _units = units; _contracts = contracts; _invoices = invoices; }

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken ct)
    {
        var props = await _props.GetAllAsync(ct);
        var units = await _units.GetAllAsync(ct);
        var contracts = await _contracts.GetAllAsync(ct);
        var invoices = await _invoices.GetAllAsync(ct);

        var now = DateTime.UtcNow;
        var threshold30 = now.AddDays(30);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var totalUnits = units.Count(u => !u.IsDeleted);
        var occupiedUnits = units.Count(u => !u.IsDeleted && u.Status == UnitStatus.Occupied);
        var activeContracts = contracts.Where(c => !c.IsDeleted && c.Status == ContractStatus.Active).ToList();
        var allInvoices = invoices.Where(i => !i.IsDeleted).ToList();

        var overdueInvoices = allInvoices.Count(i =>
            (i.Status == PaymentStatus.Overdue || (i.Status == PaymentStatus.Pending && i.DueDate < now)));

        var totalDebt = allInvoices
            .Where(i => i.Status != PaymentStatus.Paid && i.Status != PaymentStatus.Cancelled)
            .Sum(i => i.DebtAmount);

        var monthlyRevenue = allInvoices
            .Where(i => i.DueDate >= monthStart && i.Type == InvoiceType.Rent && i.Status != PaymentStatus.Cancelled)
            .Sum(i => i.Amount);

        // Revenue chart: last 6 months
        var revenueChart = Enumerable.Range(0, 6)
            .Select(i => {
                var month = now.AddMonths(-5 + i);
                var mStart = new DateTime(month.Year, month.Month, 1);
                var mEnd = mStart.AddMonths(1);
                var mInvoices = allInvoices.Where(inv => inv.DueDate >= mStart && inv.DueDate < mEnd && inv.Status != PaymentStatus.Cancelled);
                return new MonthlyRevenueDto(
                    month.ToString("MMM yy"),
                    mInvoices.Sum(inv => inv.Amount),
                    mInvoices.Sum(inv => inv.PaidAmount));
            })
            .ToList();

        return new DashboardStatsDto(
            props.Count(p => !p.IsDeleted),
            totalUnits,
            occupiedUnits,
            totalUnits == 0 ? 0 : Math.Round((double)occupiedUnits / totalUnits * 100, 1),
            activeContracts.Count,
            activeContracts.Count(c => c.EndDate <= threshold30),
            monthlyRevenue,
            totalDebt,
            overdueInvoices,
            revenueChart);
    }
}
