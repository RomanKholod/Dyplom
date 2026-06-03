using FluentValidation;
using MediatR;
using RentalManagement.Application.Common;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Interfaces;

namespace RentalManagement.Application.Features.CRM;

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

public record OwnerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? TaxCode,
    bool IsCompany,
    string? CompanyName,
    string Email,
    string Phone,
    decimal? ManagementFeePercent,
    string FullName,
    int PropertiesCount,
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

public record CreateOwnerDto(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? TaxCode,
    bool IsCompany,
    string? CompanyName,
    string Email,
    string Phone,
    decimal? ManagementFeePercent);

// ─── QUERIES ─────────────────────────────────────────────────

public record GetTenantsQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<PagedResult<TenantDto>>;

public record GetTenantByIdQuery(Guid Id)
    : IRequest<Result<TenantDto>>;

public record GetOwnersQuery(string? Search = null)
    : IRequest<IEnumerable<OwnerDto>>;

public record GetOwnerByIdQuery(Guid Id)
    : IRequest<Result<OwnerDto>>;

// ─── COMMANDS ────────────────────────────────────────────────

public record CreateTenantCommand(CreateTenantDto Data) : IRequest<Result<TenantDto>>;
public record UpdateTenantCommand(Guid Id, UpdateTenantDto Data) : IRequest<Result<TenantDto>>;
public record DeleteTenantCommand(Guid Id) : IRequest<Result>;

public record CreateOwnerCommand(CreateOwnerDto Data) : IRequest<Result<OwnerDto>>;
public record UpdateOwnerCommand(Guid Id, CreateOwnerDto Data) : IRequest<Result<OwnerDto>>;
public record DeleteOwnerCommand(Guid Id) : IRequest<Result>;

// ─── VALIDATORS ──────────────────────────────────────────────

public class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Data.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Data.Phone).NotEmpty().MaximumLength(20);
        When(x => x.Data.IsCompany, () =>
            RuleFor(x => x.Data.CompanyName).NotEmpty());
    }
}

public class CreateOwnerValidator : AbstractValidator<CreateOwnerCommand>
{
    public CreateOwnerValidator()
    {
        RuleFor(x => x.Data.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Data.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Data.ManagementFeePercent)
            .InclusiveBetween(0, 100).When(x => x.Data.ManagementFeePercent.HasValue);
    }
}

// ─── HANDLERS — TENANT ───────────────────────────────────────

public class GetTenantsHandler : IRequestHandler<GetTenantsQuery, PagedResult<TenantDto>>
{
    private readonly IRepository<Tenant> _repo;
    public GetTenantsHandler(IRepository<Tenant> repo) => _repo = repo;

    public async Task<PagedResult<TenantDto>> Handle(GetTenantsQuery q, CancellationToken ct)
    {
        var all = await _repo.GetAllAsync(ct);
        var filtered = all.Where(t => !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q.Search))
            filtered = filtered.Where(t =>
                t.FirstName.Contains(q.Search, StringComparison.OrdinalIgnoreCase) ||
                t.LastName.Contains(q.Search, StringComparison.OrdinalIgnoreCase) ||
                t.Email.Contains(q.Search, StringComparison.OrdinalIgnoreCase) ||
                t.Phone.Contains(q.Search, StringComparison.OrdinalIgnoreCase) ||
                (t.CompanyName != null && t.CompanyName.Contains(q.Search, StringComparison.OrdinalIgnoreCase)));

        var total = filtered.Count();
        var items = filtered
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(MapToDto)
            .ToList();

        return new PagedResult<TenantDto> { Items = items, TotalCount = total, Page = q.Page, PageSize = q.PageSize };
    }

    private static TenantDto MapToDto(Tenant t) => new(
        t.Id, t.FirstName, t.LastName, t.MiddleName, t.TaxCode, t.PassportNumber,
        t.IsCompany, t.CompanyName, t.CompanyCode, t.Email, t.Phone, t.Notes,
        t.FullName, t.Contracts.Count(c => c.Status == Domain.Enums.ContractStatus.Active),
        t.CreatedAt);
}

public class GetTenantByIdHandler : IRequestHandler<GetTenantByIdQuery, Result<TenantDto>>
{
    private readonly IRepository<Tenant> _repo;
    public GetTenantByIdHandler(IRepository<Tenant> repo) => _repo = repo;

    public async Task<Result<TenantDto>> Handle(GetTenantByIdQuery q, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(q.Id, ct);
        if (t is null || t.IsDeleted) return Result<TenantDto>.Failure("Tenant not found");
        return Result<TenantDto>.Success(new TenantDto(
            t.Id, t.FirstName, t.LastName, t.MiddleName, t.TaxCode, t.PassportNumber,
            t.IsCompany, t.CompanyName, t.CompanyCode, t.Email, t.Phone, t.Notes,
            t.FullName, t.Contracts.Count(c => c.Status == Domain.Enums.ContractStatus.Active),
            t.CreatedAt));
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
            tenant.TaxCode, tenant.PassportNumber, tenant.IsCompany, tenant.CompanyName,
            tenant.CompanyCode, tenant.Email, tenant.Phone, tenant.Notes,
            tenant.FullName, 0, tenant.CreatedAt));
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
        if (tenant is null || tenant.IsDeleted) return Result<TenantDto>.Failure("Tenant not found");

        var d = cmd.Data;
        tenant.FirstName = d.FirstName; tenant.LastName = d.LastName; tenant.MiddleName = d.MiddleName;
        tenant.TaxCode = d.TaxCode; tenant.PassportNumber = d.PassportNumber;
        tenant.IsCompany = d.IsCompany; tenant.CompanyName = d.CompanyName; tenant.CompanyCode = d.CompanyCode;
        tenant.Email = d.Email; tenant.Phone = d.Phone; tenant.Notes = d.Notes;

        _repo.Update(tenant);
        await _uow.SaveChangesAsync(ct);
        return Result<TenantDto>.Success(new TenantDto(
            tenant.Id, tenant.FirstName, tenant.LastName, tenant.MiddleName,
            tenant.TaxCode, tenant.PassportNumber, tenant.IsCompany, tenant.CompanyName,
            tenant.CompanyCode, tenant.Email, tenant.Phone, tenant.Notes,
            tenant.FullName, tenant.Contracts.Count(c => c.Status == Domain.Enums.ContractStatus.Active),
            tenant.CreatedAt));
    }
}

public class DeleteTenantHandler : IRequestHandler<DeleteTenantCommand, Result>
{
    private readonly IRepository<Tenant> _repo;
    private readonly IUnitOfWork _uow;
    public DeleteTenantHandler(IRepository<Tenant> repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(DeleteTenantCommand cmd, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(cmd.Id, ct);
        if (tenant is null) return Result.Failure("Tenant not found");
        if (tenant.Contracts.Any(c => c.Status == Domain.Enums.ContractStatus.Active))
            return Result.Failure("Cannot delete tenant with active contracts");

        tenant.IsDeleted = true;
        _repo.Update(tenant);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── HANDLERS — OWNER ────────────────────────────────────────

public class GetOwnersHandler : IRequestHandler<GetOwnersQuery, IEnumerable<OwnerDto>>
{
    private readonly IRepository<Owner> _repo;
    public GetOwnersHandler(IRepository<Owner> repo) => _repo = repo;

    public async Task<IEnumerable<OwnerDto>> Handle(GetOwnersQuery q, CancellationToken ct)
    {
        var all = await _repo.GetAllAsync(ct);
        var filtered = all.Where(o => !o.IsDeleted);
        if (!string.IsNullOrWhiteSpace(q.Search))
            filtered = filtered.Where(o =>
                o.FirstName.Contains(q.Search, StringComparison.OrdinalIgnoreCase) ||
                o.LastName.Contains(q.Search, StringComparison.OrdinalIgnoreCase) ||
                o.Email.Contains(q.Search, StringComparison.OrdinalIgnoreCase));
        return filtered.OrderBy(o => o.LastName).Select(MapToDto).ToList();
    }

    private static OwnerDto MapToDto(Owner o) => new(
        o.Id, o.FirstName, o.LastName, o.MiddleName, o.TaxCode,
        o.IsCompany, o.CompanyName, o.Email, o.Phone, o.ManagementFeePercent,
        o.FullName, o.Properties.Count, o.CreatedAt);
}

public class GetOwnerByIdHandler : IRequestHandler<GetOwnerByIdQuery, Result<OwnerDto>>
{
    private readonly IRepository<Owner> _repo;
    public GetOwnerByIdHandler(IRepository<Owner> repo) => _repo = repo;

    public async Task<Result<OwnerDto>> Handle(GetOwnerByIdQuery q, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(q.Id, ct);
        if (o is null || o.IsDeleted) return Result<OwnerDto>.Failure("Owner not found");
        return Result<OwnerDto>.Success(new OwnerDto(
            o.Id, o.FirstName, o.LastName, o.MiddleName, o.TaxCode,
            o.IsCompany, o.CompanyName, o.Email, o.Phone, o.ManagementFeePercent,
            o.FullName, o.Properties.Count, o.CreatedAt));
    }
}

public class CreateOwnerHandler : IRequestHandler<CreateOwnerCommand, Result<OwnerDto>>
{
    private readonly IRepository<Owner> _repo;
    private readonly IUnitOfWork _uow;
    public CreateOwnerHandler(IRepository<Owner> repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result<OwnerDto>> Handle(CreateOwnerCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;
        var owner = new Owner
        {
            FirstName = d.FirstName, LastName = d.LastName, MiddleName = d.MiddleName,
            TaxCode = d.TaxCode, IsCompany = d.IsCompany, CompanyName = d.CompanyName,
            Email = d.Email, Phone = d.Phone, ManagementFeePercent = d.ManagementFeePercent
        };
        await _repo.AddAsync(owner, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<OwnerDto>.Success(new OwnerDto(
            owner.Id, owner.FirstName, owner.LastName, owner.MiddleName, owner.TaxCode,
            owner.IsCompany, owner.CompanyName, owner.Email, owner.Phone,
            owner.ManagementFeePercent, owner.FullName, 0, owner.CreatedAt));
    }
}
