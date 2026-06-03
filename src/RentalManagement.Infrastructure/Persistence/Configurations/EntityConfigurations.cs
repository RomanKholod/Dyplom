using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentalManagement.Domain.Entities;

namespace RentalManagement.Infrastructure.Persistence.Configurations;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Address).HasMaxLength(500).IsRequired();
        b.Property(p => p.City).HasMaxLength(100).IsRequired();
        b.Property(p => p.Type).HasConversion<string>();
        b.Property(p => p.Status).HasConversion<string>();

        b.HasOne(p => p.Owner)
            .WithMany(o => o.Properties)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(p => p.Units)
            .WithOne(u => u.Property)
            .HasForeignKey(u => u.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(p => p.City);
        b.HasIndex(p => p.Status);
    }
}

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Number).HasMaxLength(20).IsRequired();
        b.Property(u => u.Area).HasPrecision(10, 2);
        b.Property(u => u.BaseRentPrice).HasPrecision(18, 2);
        b.Property(u => u.Status).HasConversion<string>();
    }
}

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.FirstName).HasMaxLength(100).IsRequired();
        b.Property(t => t.LastName).HasMaxLength(100).IsRequired();
        b.Property(t => t.Email).HasMaxLength(255).IsRequired();
        b.Property(t => t.Phone).HasMaxLength(20).IsRequired();
        b.HasIndex(t => t.Email).IsUnique();
        b.HasIndex(t => t.TaxCode);
    }
}

public class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    public void Configure(EntityTypeBuilder<Owner> b)
    {
        b.HasKey(o => o.Id);
        b.Property(o => o.FirstName).HasMaxLength(100).IsRequired();
        b.Property(o => o.LastName).HasMaxLength(100).IsRequired();
        b.Property(o => o.Email).HasMaxLength(255).IsRequired();
        b.Property(o => o.Phone).HasMaxLength(20).IsRequired();
        b.Property(o => o.ManagementFeePercent).HasPrecision(5, 2);
        b.HasIndex(o => o.Email).IsUnique();
    }
}

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Number).HasMaxLength(50).IsRequired();
        b.Property(c => c.MonthlyRent).HasPrecision(18, 2);
        b.Property(c => c.SecurityDeposit).HasPrecision(18, 2);
        b.Property(c => c.Status).HasConversion<string>();

        b.HasOne(c => c.Unit)
            .WithMany(u => u.Contracts)
            .HasForeignKey(c => c.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(c => c.Tenant)
            .WithMany(t => t.Contracts)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(c => c.Number).IsUnique();
        b.HasIndex(c => c.Status);
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.Number).HasMaxLength(50).IsRequired();
        b.Property(i => i.Amount).HasPrecision(18, 2);
        b.Property(i => i.PaidAmount).HasPrecision(18, 2);
        b.Property(i => i.Type).HasConversion<string>();
        b.Property(i => i.Status).HasConversion<string>();

        b.HasOne(i => i.Contract)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Ignore(i => i.DebtAmount);
        b.HasIndex(i => i.Status);
        b.HasIndex(i => i.DueDate);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Amount).HasPrecision(18, 2);
        b.Property(p => p.Reference).HasMaxLength(100);

        b.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MeterConfiguration : IEntityTypeConfiguration<Meter>
{
    public void Configure(EntityTypeBuilder<Meter> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Number).HasMaxLength(50).IsRequired();
        b.Property(m => m.Type).HasMaxLength(30).IsRequired();
        b.Property(m => m.CurrentReading).HasPrecision(12, 3);
        b.Property(m => m.RatePerUnit).HasPrecision(10, 4);

        b.HasOne(m => m.Unit)
            .WithMany(u => u.Meters)
            .HasForeignKey(m => m.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Reading).HasPrecision(12, 3);
        b.Property(r => r.Consumption).HasPrecision(12, 3);
        b.Property(r => r.Amount).HasPrecision(18, 2);

        b.HasOne(r => r.Meter)
            .WithMany(m => m.Readings)
            .HasForeignKey(r => r.MeterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
