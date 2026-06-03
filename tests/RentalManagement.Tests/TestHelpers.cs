using Bogus;
using Microsoft.EntityFrameworkCore;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Infrastructure.Persistence;

namespace RentalManagement.Tests;

public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}

public static class Builders
{
    private static readonly Faker _faker = new("uk");

    public static Owner BuildOwner(Action<Owner>? configure = null)
    {
        var o = new Owner
        {
            FirstName = _faker.Name.FirstName(),
            LastName  = _faker.Name.LastName(),
            Email     = _faker.Internet.Email(),
            Phone     = _faker.Phone.PhoneNumber("+380#########"),
            IsCompany = false,
        };
        configure?.Invoke(o);
        return o;
    }

    public static Property BuildProperty(Guid ownerId, Action<Property>? configure = null)
    {
        var p = new Property
        {
            Name        = _faker.Company.CompanyName(),
            Address     = _faker.Address.StreetAddress(),
            City        = _faker.Address.City(),
            Type        = PropertyType.Commercial,
            Status      = PropertyStatus.Available,
            TotalArea   = _faker.Random.Int(100, 2000),
            FloorsCount = _faker.Random.Int(1, 10),
            OwnerId     = ownerId,
        };
        configure?.Invoke(p);
        return p;
    }

    public static Unit BuildUnit(Guid propertyId, Action<Unit>? configure = null)
    {
        var u = new Unit
        {
            Number        = _faker.Random.AlphaNumeric(3).ToUpper(),
            Floor         = _faker.Random.Int(1, 5),
            Area          = _faker.Random.Decimal(20, 200),
            RoomsCount    = _faker.Random.Int(1, 5),
            BaseRentPrice = _faker.Random.Decimal(5000, 50000),
            Status        = UnitStatus.Available,
            PropertyId    = propertyId,
        };
        configure?.Invoke(u);
        return u;
    }

    public static Tenant BuildTenant(Action<Tenant>? configure = null)
    {
        var t = new Tenant
        {
            FirstName = _faker.Name.FirstName(),
            LastName  = _faker.Name.LastName(),
            Email     = _faker.Internet.Email(),
            Phone     = _faker.Phone.PhoneNumber("+380#########"),
            IsCompany = false,
        };
        configure?.Invoke(t);
        return t;
    }

    public static Contract BuildContract(Guid unitId, Guid tenantId, Action<Contract>? configure = null)
    {
        var c = new Contract
        {
            Number             = $"ДО-{DateTime.UtcNow.Year}-{_faker.Random.Int(1, 9999):D4}",
            UnitId             = unitId,
            TenantId           = tenantId,
            StartDate          = DateTime.UtcNow.Date,
            EndDate            = DateTime.UtcNow.Date.AddYears(1),
            MonthlyRent        = _faker.Random.Decimal(5000, 30000),
            SecurityDeposit    = _faker.Random.Decimal(5000, 30000),
            PaymentDayOfMonth  = _faker.Random.Int(1, 28),
            Status             = ContractStatus.Draft,
        };
        configure?.Invoke(c);
        return c;
    }

    public static Invoice BuildInvoice(Guid contractId, Action<Invoice>? configure = null)
    {
        var i = new Invoice
        {
            Number      = $"РХ-{DateTime.UtcNow:yyyyMM}-{_faker.Random.Int(1, 9999):D4}",
            ContractId  = contractId,
            Type        = InvoiceType.Rent,
            Amount      = _faker.Random.Decimal(5000, 30000),
            DueDate     = DateTime.UtcNow.Date.AddDays(5),
            Status      = PaymentStatus.Pending,
        };
        configure?.Invoke(i);
        return i;
    }
}
