using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentalManagement.Domain.Entities;
using RentalManagement.Infrastructure.Identity;

namespace RentalManagement.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    // Єдиний правильний конструктор, який використовує DI контейнер
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Properties module
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();

    // CRM module
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Owner> Owners => Set<Owner>();

    // Contracts module
    public DbSet<Contract> Contracts => Set<Contract>();

    // Finance module
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Utilities module
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter: soft delete
        builder.Entity<Property>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<Unit>().HasQueryFilter(u => !u.IsDeleted);
        builder.Entity<Tenant>().HasQueryFilter(t => !t.IsDeleted);
        builder.Entity<Owner>().HasQueryFilter(o => !o.IsDeleted);
        builder.Entity<Contract>().HasQueryFilter(c => !c.IsDeleted);
        builder.Entity<Invoice>().HasQueryFilter(i => !i.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}