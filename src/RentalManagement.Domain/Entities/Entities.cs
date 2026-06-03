using RentalManagement.Domain.Common;
using RentalManagement.Domain.Enums;

namespace RentalManagement.Domain.Entities;

// ─── PROPERTY ───────────────────────────────────────────────

public class Property : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public PropertyType Type { get; set; }
    public PropertyStatus Status { get; set; } = PropertyStatus.Available;
    public int TotalArea { get; set; }
    public int FloorsCount { get; set; }
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }

    // Navigation
    public Owner Owner { get; set; } = null!;
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}

public class Unit : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    public decimal Area { get; set; }
    public int RoomsCount { get; set; }
    public decimal BaseRentPrice { get; set; }
    public UnitStatus Status { get; set; } = UnitStatus.Available;
    public string? Description { get; set; }
    public Guid PropertyId { get; set; }

    // Navigation
    public Property Property { get; set; } = null!;
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<Meter> Meters { get; set; } = new List<Meter>();
}

// ─── CRM ────────────────────────────────────────────────────

public class Tenant : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? TaxCode { get; set; }
    public string? PassportNumber { get; set; }
    public bool IsCompany { get; set; } = false;
    public string? CompanyName { get; set; }
    public string? CompanyCode { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public string FullName => IsCompany
        ? CompanyName ?? string.Empty
        : $"{LastName} {FirstName} {MiddleName}".Trim();

    // Navigation
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}

public class Owner : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? TaxCode { get; set; }
    public bool IsCompany { get; set; } = false;
    public string? CompanyName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal? ManagementFeePercent { get; set; }

    public string FullName => IsCompany
        ? CompanyName ?? string.Empty
        : $"{LastName} {FirstName} {MiddleName}".Trim();

    // Navigation
    public ICollection<Property> Properties { get; set; } = new List<Property>();
}

// ─── CONTRACTS ──────────────────────────────────────────────

public class Contract : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid UnitId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal SecurityDeposit { get; set; }
    public int PaymentDayOfMonth { get; set; } = 1;
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public string? TerminationReason { get; set; }
    public DateTime? TerminatedAt { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Unit Unit { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

// ─── FINANCE ────────────────────────────────────────────────

public class Invoice : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid ContractId { get; set; }
    public InvoiceType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; } = 0;
    public DateTime DueDate { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public string? Description { get; set; }

    public decimal DebtAmount => Amount - PaidAmount;

    // Navigation
    public Contract Contract { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class Payment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;
}

// ─── UTILITIES ──────────────────────────────────────────────

public class Meter : BaseEntity
{
    public string Number { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // electricity, gas, water, heating
    public Guid UnitId { get; set; }
    public decimal CurrentReading { get; set; }
    public DateTime LastReadingDate { get; set; }
    public decimal RatePerUnit { get; set; }

    // Navigation
    public Unit Unit { get; set; } = null!;
    public ICollection<MeterReading> Readings { get; set; } = new List<MeterReading>();
}

public class MeterReading : BaseEntity
{
    public Guid MeterId { get; set; }
    public decimal Reading { get; set; }
    public DateTime ReadingDate { get; set; }
    public decimal Consumption { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Meter Meter { get; set; } = null!;
}
