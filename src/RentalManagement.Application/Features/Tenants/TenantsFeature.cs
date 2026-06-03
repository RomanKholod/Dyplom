using FluentValidation;
using MediatR;
using RentalManagement.Application.Common;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Interfaces;

namespace RentalManagement.Application.Features.Tenants;

// ─── DTOs ────────────────────────────────────────────────────

public record TenantDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? TaxCode,
    string? PassportNumber,
    bool IsCompany,
    string? CompanyName,
    string? CompanyCode,
    string Email,
    string Phone,
    string? Notes,
    string FullName,
    int ActiveContractsCount,
    DateTime CreatedAt);

public record CreateTenantDto(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? TaxCode,
    string? PassportNumber,
    bool IsCompany,
    string? CompanyName,
    string? CompanyCode,
    string Email,
    string Phone,
    string? Notes);

public record UpdateTenantDto(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? TaxCode,
    string? PassportNumber,
    bool IsCompany,
    string? CompanyName,
    string? CompanyCode,
    string Email,
    string Phone,
    string? Notes);

// ─── QUERIES ─────────────────────────────────────────────────

public record GetTenantsQuery(int Page = 1, int PageSize = 20, string? Search = null, bool? IsCompany = null)
    : IRequest<PagedResult<TenantDto>>;

public record GetTenantByIdQuery(Guid Id)
    : IRequest<Result<TenantDto>>;

// ─── COMMANDS ────────────────────────────────────────────────

public record CreateTenantCommand(CreateTenantDto Data)
    : IRequest<Result<TenantDto>>;

public record UpdateTenantCommand(Guid Id, UpdateTenantDto Data)
    : IRequest<Result<TenantDto>>;

public record DeleteTenantCommand(Guid Id)
    : IRequest<Result>;

// ─── VALIDATORS ──────────────────────────────────────────────

public class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Data.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Data.Phone).NotEmpty().MaximumLength(20);
        When(x => x.Data.IsCompany, () => {
            RuleFor(x => x.Data.CompanyName).NotEmpty().MaximumLength(200);
        });
    }
}

public class UpdateTenantValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantValidator()
    {
        RuleFor(x => x.Data.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Data.Phone).NotEmpty().MaximumLength(20);
    }
}

// ─── HANDLERS ────────────────────────────────────────────────

public class GetTenantsHandler : IRequestHandler<GetTenantsQuery, PagedResult<TenantDto>>
{
    private readonly IRepository<Tenant> _repo;
    private readonly IRepository<Contract> _contracts;

    public GetTenantsHandler(IRepository<Tenant> repo, IRepository<Contract> contracts)
    {
        _repo = repo;
        _contracts = contracts;
    }

    public async Task<PagedResult<TenantDto>> Handle(GetTenantsQuery request, CancellationToken ct)
    {
        var all = await _repo.GetAllAsync(ct);
        var filtered = all.Where(t => !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
            filtered = filtered.Where(t =>
                t.FirstName.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                t.LastName.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                t.Email.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                t.Phone.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                (t.CompanyName != null && t.CompanyName.Contains(request.Search, StringComparison.OrdinalIgnoreCase)));

        if (request.IsCompany.HasValue)
            filtered = filtered.Where(t => t.IsCompany == request.IsCompany.Value);

        var allContracts = await _contracts.GetAllAsync(ct);
        var activeContractsByTenant = allContracts
            .Where(c => c.Status == Domain.Enums.ContractStatus.Active)
            .GroupBy(c => c.TenantId)
            .ToDictionary(g => g.Key, g => g.Count());

        var total = filtered.Count();
        var items = filtered
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => MapToDto(t, activeContractsByTenant.GetValueOrDefault(t.Id, 0)))
            .ToList();

        return new PagedResult<TenantDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }

    private static TenantDto MapToDto(Tenant t, int activeContracts) => new(
        t.Id, t.FirstName, t.LastName, t.MiddleName,
        t.TaxCode, t.PassportNumber, t.IsCompany,
        t.CompanyName, t.CompanyCode, t.Email, t.Phone,
        t.Notes, t.FullName, activeContracts, t.CreatedAt);
}

public class GetTenantByIdHandler : IRequestHandler<GetTenantByIdQuery, Result<TenantDto>>
{
    private readonly IRepository<Tenant> _repo;

    public GetTenantByIdHandler(IRepository<Tenant> repo) => _repo = repo;

    public async Task<Result<TenantDto>> Handle(GetTenantByIdQuery request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct);
        if (tenant == null || tenant.IsDeleted)
            return Result<TenantDto>.Failure("Орендаря не знайдено");

        return Result<TenantDto>.Success(new TenantDto(
            tenant.Id, tenant.FirstName, tenant.LastName, tenant.MiddleName,
            tenant.TaxCode, tenant.PassportNumber, tenant.IsCompany,
            tenant.CompanyName, tenant.CompanyCode, tenant.Email, tenant.Phone,
            tenant.Notes, tenant.FullName, 0, tenant.CreatedAt));
    }
}

public class CreateTenantHandler : IRequestHandler<CreateTenantCommand, Result<TenantDto>>
{
    private readonly IRepository<Tenant> _repo;
    private readonly IUnitOfWork _uow;

    public CreateTenantHandler(IRepository<Tenant> repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<TenantDto>> Handle(CreateTenantCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;
        var tenant = new Tenant
        {
            FirstName = d.FirstName, LastName = d.LastName, MiddleName = d.MiddleName,
            TaxCode = d.TaxCode, PassportNumber = d.PassportNumber,
            IsCompany = d.IsCompany, CompanyName = d.CompanyName, CompanyCode = d.CompanyCode,
            Email = d.Email, Phone = d.Phone, Notes = d.Notes
        };
        await _repo.AddAsync(tenant, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<TenantDto>.Success(new TenantDto(
            tenant.Id, tenant.FirstName, tenant.LastName, tenant.MiddleName,
            tenant.TaxCode, tenant.PassportNumber, tenant.IsCompany,
            tenant.CompanyName, tenant.CompanyCode, tenant.Email, tenant.Phone,
            tenant.Notes, tenant.FullName, 0, tenant.CreatedAt));
    }
}

public class UpdateTenantHandler : IRequestHandler<UpdateTenantCommand, Result<TenantDto>>
{
    private readonly IRepository<Tenant> _repo;
    private readonly IUnitOfWork _uow;

    public UpdateTenantHandler(IRepository<Tenant> repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<TenantDto>> Handle(UpdateTenantCommand cmd, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(cmd.Id, ct);
        if (tenant == null || tenant.IsDeleted)
            return Result<TenantDto>.Failure("Орендаря не знайдено");

        var d = cmd.Data;
        tenant.FirstName = d.FirstName; tenant.LastName = d.LastName; tenant.MiddleName = d.MiddleName;
        tenant.TaxCode = d.TaxCode; tenant.PassportNumber = d.PassportNumber;
        tenant.IsCompany = d.IsCompany; tenant.CompanyName = d.CompanyName; tenant.CompanyCode = d.CompanyCode;
        tenant.Email = d.Email; tenant.Phone = d.Phone; tenant.Notes = d.Notes;
        tenant.UpdatedAt = DateTime.UtcNow;

        _repo.Update(tenant);
        await _uow.SaveChangesAsync(ct);
        return Result<TenantDto>.Success(new TenantDto(
            tenant.Id, tenant.FirstName, tenant.LastName, tenant.MiddleName,
            tenant.TaxCode, tenant.PassportNumber, tenant.IsCompany,
            tenant.CompanyName, tenant.CompanyCode, tenant.Email, tenant.Phone,
            tenant.Notes, tenant.FullName, 0, tenant.CreatedAt));
    }
}

public class DeleteTenantHandler : IRequestHandler<DeleteTenantCommand, Result>
{
    private readonly IRepository<Tenant> _repo;
    private readonly IRepository<Contract> _contracts;
    private readonly IUnitOfWork _uow;

    public DeleteTenantHandler(IRepository<Tenant> repo, IRepository<Contract> contracts, IUnitOfWork uow)
    { _repo = repo; _contracts = contracts; _uow = uow; }

    public async Task<Result> Handle(DeleteTenantCommand cmd, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(cmd.Id, ct);
        if (tenant == null) return Result.Failure("Орендаря не знайдено");

        var active = await _contracts.FindAsync(
            c => c.TenantId == cmd.Id && c.Status == Domain.Enums.ContractStatus.Active, ct);
        if (active.Any())
            return Result.Failure("Неможливо видалити орендаря з активними договорами");

        tenant.IsDeleted = true;
        _repo.Update(tenant);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
