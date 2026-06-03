using RentalManagement.Application.Features.Contracts;
using RentalManagement.Application.Features.Invoices;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Interfaces;
using RentalManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RentalManagement.Infrastructure.Services;

public class ContractNumberService : IContractNumberGenerator
{
    private readonly AppDbContext _db;

    public ContractNumberService(AppDbContext db) => _db = db;

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.Contracts.CountAsync(c => c.CreatedAt.Year == year, ct);
        return $"ДО-{year}-{(count + 1):D4}";
    }

    public async Task<string> GenerateNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.Contracts.CountAsync(c => c.CreatedAt.Year == year, ct);
        return $"ДО-{year}-{(count + 1):D4}";
    }
}

public class InvoiceNumberService : IInvoiceNumberService
{
    private readonly AppDbContext _db;

    public InvoiceNumberService(AppDbContext db) => _db = db;

    public async Task<string> GenerateNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var month = DateTime.UtcNow.Month;
        var count = await _db.Invoices.CountAsync(i => i.CreatedAt.Year == year && i.CreatedAt.Month == month, ct);
        return $"РХ-{year}{month:D2}-{(count + 1):D4}";
    }
}
