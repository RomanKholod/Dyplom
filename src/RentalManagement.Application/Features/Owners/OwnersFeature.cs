using FluentValidation;
using MediatR;
using RentalManagement.Application.Common;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Interfaces;

namespace RentalManagement.Application.Features.Owners;

// ─── DTOs ────────────────────────────────────────────────────

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

public record GetOwnersQuery(int Page = 1, int PageSize = 50, string? Search = null)
    : IRequest<PagedResult<OwnerDto>>;

public record GetOwnerByIdQuery(Guid Id)
    : IRequest<Result<OwnerDto>>;

// ─── COMMANDS ────────────────────────────────────────────────

public record CreateOwnerCommand(CreateOwnerDto Data)
    : IRequest<Result<OwnerDto>>;

public record UpdateOwnerCommand(Guid Id, CreateOwnerDto Data)
    : IRequest<Result<OwnerDto>>;

public record DeleteOwnerCommand(Guid Id)
    : IRequest<Result>;

// ─── VALIDATORS ──────────────────────────────────────────────

public class CreateOwnerValidator : AbstractValidator<CreateOwnerCommand>
{
    public CreateOwnerValidator()
    {
        RuleFor(x => x.Data.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Data.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Data.ManagementFeePercent)
            .InclusiveBetween(0, 100).When(x => x.Data.ManagementFeePercent.HasValue);
    }
}

// ─── HANDLERS ────────────────────────────────────────────────

public class GetOwnersHandler : IRequestHandler<GetOwnersQuery, PagedResult<OwnerDto>>
{
    private readonly IRepository<Owner> _repo;
    private readonly IRepository<Property> _props;

    public GetOwnersHandler(IRepository<Owner> repo, IRepository<Property> props) { _repo = repo; _props = props; }

    public async Task<PagedResult<OwnerDto>> Handle(GetOwnersQuery request, CancellationToken ct)
    {
        var all = await _repo.GetAllAsync(ct);
        var filtered = all.Where(o => !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
            filtered = filtered.Where(o =>
                o.FirstName.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                o.LastName.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                o.Email.Contains(request.Search, StringComparison.OrdinalIgnoreCase));

        var props = await _props.GetAllAsync(ct);
        var propsByOwner = props.GroupBy(p => p.OwnerId).ToDictionary(g => g.Key, g => g.Count());

        var total = filtered.Count();
        var items = filtered
            .OrderByDescending(o => o.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => MapToDto(o, propsByOwner.GetValueOrDefault(o.Id, 0)))
            .ToList();

        return new PagedResult<OwnerDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }

    private static OwnerDto MapToDto(Owner o, int propsCount) => new(
        o.Id, o.FirstName, o.LastName, o.MiddleName, o.TaxCode,
        o.IsCompany, o.CompanyName, o.Email, o.Phone,
        o.ManagementFeePercent, o.FullName, propsCount, o.CreatedAt);
}

public class GetOwnerByIdHandler : IRequestHandler<GetOwnerByIdQuery, Result<OwnerDto>>
{
    private readonly IRepository<Owner> _repo;
    public GetOwnerByIdHandler(IRepository<Owner> repo) => _repo = repo;

    public async Task<Result<OwnerDto>> Handle(GetOwnerByIdQuery request, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(request.Id, ct);
        if (o == null || o.IsDeleted) return Result<OwnerDto>.Failure("Власника не знайдено");
        return Result<OwnerDto>.Success(MapToDto(o, 0));
    }

    private static OwnerDto MapToDto(Owner o, int p) => new(
        o.Id, o.FirstName, o.LastName, o.MiddleName, o.TaxCode,
        o.IsCompany, o.CompanyName, o.Email, o.Phone,
        o.ManagementFeePercent, o.FullName, p, o.CreatedAt);
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

public class DeleteOwnerHandler : IRequestHandler<DeleteOwnerCommand, Result>
{
    private readonly IRepository<Owner> _repo;
    private readonly IRepository<Property> _props;
    private readonly IUnitOfWork _uow;
    public DeleteOwnerHandler(IRepository<Owner> repo, IRepository<Property> props, IUnitOfWork uow)
    { _repo = repo; _props = props; _uow = uow; }

    public async Task<Result> Handle(DeleteOwnerCommand cmd, CancellationToken ct)
    {
        var owner = await _repo.GetByIdAsync(cmd.Id, ct);
        if (owner == null) return Result.Failure("Власника не знайдено");

        var ownedProps = await _props.FindAsync(p => p.OwnerId == cmd.Id && !p.IsDeleted, ct);
        if (ownedProps.Any()) return Result.Failure("Неможливо видалити власника з активними об'єктами");

        owner.IsDeleted = true;
        _repo.Update(owner);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
