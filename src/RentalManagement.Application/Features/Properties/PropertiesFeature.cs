using FluentValidation;
using MediatR;
using RentalManagement.Application.Common;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Domain.Interfaces;
using Unit = RentalManagement.Domain.Entities.Unit; // Уникаємо конфлікту з MediatR.Unit

namespace RentalManagement.Application.Features.Properties;

// ─── DTOs ────────────────────────────────────────────────────

public record PropertyDto(
    Guid Id,
    string Name,
    string Address,
    string City,
    string Type,
    string Status,
    int TotalArea,
    int FloorsCount,
    string? Description,
    Guid OwnerId,
    string OwnerName,
    int UnitsCount,
    int OccupiedUnitsCount,
    DateTime CreatedAt);

public record UnitDto(
    Guid Id,
    string Number,
    int Floor,
    decimal Area,
    int RoomsCount,
    decimal BaseRentPrice,
    string Status,
    Guid PropertyId,
    string PropertyName);

public record CreatePropertyDto(
    string Name,
    string Address,
    string City,
    PropertyType Type,
    int TotalArea,
    int FloorsCount,
    Guid OwnerId,
    string? Description);

public record UpdatePropertyDto(
    string Name,
    string Address,
    string City,
    PropertyType Type,
    int TotalArea,
    int FloorsCount,
    PropertyStatus Status,
    string? Description);

public record CreateUnitDto(
    string Number,
    int Floor,
    decimal Area,
    int RoomsCount,
    decimal BaseRentPrice,
    Guid PropertyId,
    string? Description);

// ─── QUERIES ─────────────────────────────────────────────────

public record GetPropertiesQuery(int Page = 1, int PageSize = 20, string? Search = null, PropertyStatus? Status = null)
    : IRequest<PagedResult<PropertyDto>>;

public record GetPropertyByIdQuery(Guid Id)
    : IRequest<Result<PropertyDto>>;

public record GetUnitsByPropertyQuery(Guid PropertyId)
    : IRequest<IEnumerable<UnitDto>>;

// ─── COMMANDS ────────────────────────────────────────────────

public record CreatePropertyCommand(
    string Name, string Address, string City,
    PropertyType Type, int TotalArea, int FloorsCount,
    Guid OwnerId, string? Description)
    : IRequest<Result<PropertyDto>>;

public record UpdatePropertyCommand(Guid Id, UpdatePropertyDto Data)
    : IRequest<Result<PropertyDto>>;

public record DeletePropertyCommand(Guid Id)
    : IRequest<Result>;

public record CreateUnitCommand(CreateUnitDto Data)
    : IRequest<Result<UnitDto>>;

// ─── VALIDATORS ──────────────────────────────────────────────

public class CreatePropertyValidator : AbstractValidator<CreatePropertyCommand>
{
    public CreatePropertyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TotalArea).GreaterThan(0);
        RuleFor(x => x.FloorsCount).GreaterThan(0);
        RuleFor(x => x.OwnerId).NotEmpty();
    }
}

public class CreateUnitValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitValidator()
    {
        RuleFor(x => x.Data.Number).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Data.Area).GreaterThan(0);
        RuleFor(x => x.Data.BaseRentPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Data.PropertyId).NotEmpty();
    }
}

// ─── HANDLERS ────────────────────────────────────────────────

public class GetPropertiesHandler : IRequestHandler<GetPropertiesQuery, PagedResult<PropertyDto>>
{
    private readonly IRepository<Property> _repo;

    public GetPropertiesHandler(IRepository<Property> repo) => _repo = repo;

    public async Task<PagedResult<PropertyDto>> Handle(GetPropertiesQuery request, CancellationToken ct)
    {
        var all = await _repo.GetAllAsync(ct);
        var filtered = all.Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
            filtered = filtered.Where(p =>
                p.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                p.Address.Contains(request.Search, StringComparison.OrdinalIgnoreCase));

        if (request.Status.HasValue)
            filtered = filtered.Where(p => p.Status == request.Status.Value);

        var total = filtered.Count();
        var items = filtered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => MapToDto(p))
            .ToList();

        return new PagedResult<PropertyDto>
        {
            Items = items,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private static PropertyDto MapToDto(Property p) => new(
        p.Id, p.Name, p.Address, p.City,
        p.Type.ToString(), p.Status.ToString(),
        p.TotalArea, p.FloorsCount, p.Description,
        p.OwnerId, p.Owner?.FullName ?? "",
        p.Units.Count, p.Units.Count(u => u.Status == UnitStatus.Occupied),
        p.CreatedAt);
}

public class CreatePropertyHandler : IRequestHandler<CreatePropertyCommand, Result<PropertyDto>>
{
    private readonly IRepository<Property> _repo;
    private readonly IUnitOfWork _uow;

    public CreatePropertyHandler(IRepository<Property> repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Result<PropertyDto>> Handle(CreatePropertyCommand cmd, CancellationToken ct)
    {
        var property = new Property
        {
            Name = cmd.Name,
            Address = cmd.Address,
            City = cmd.City,
            Type = cmd.Type,
            TotalArea = cmd.TotalArea,
            FloorsCount = cmd.FloorsCount,
            OwnerId = cmd.OwnerId,
            Description = cmd.Description
        };

        await _repo.AddAsync(property, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<PropertyDto>.Success(new PropertyDto(
            property.Id, property.Name, property.Address, property.City,
            property.Type.ToString(), property.Status.ToString(),
            property.TotalArea, property.FloorsCount, property.Description,
            property.OwnerId, "", 0, 0, property.CreatedAt));
    }
}

public class DeletePropertyHandler : IRequestHandler<DeletePropertyCommand, Result>
{
    private readonly IRepository<Property> _repo;
    private readonly IUnitOfWork _uow;

    public DeletePropertyHandler(IRepository<Property> repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Result> Handle(DeletePropertyCommand cmd, CancellationToken ct)
    {
        var property = await _repo.GetByIdAsync(cmd.Id, ct);
        if (property is null) return Result.Failure("Property not found");

        property.IsDeleted = true;
        _repo.Update(property);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ─── ДОДАНІ ХЕНДЛЕРИ ДЛЯ ПРИМІЩЕНЬ (UNITS) ТА СУБ-ЗАПИТІВ ───

public class GetPropertyByIdHandler : IRequestHandler<GetPropertyByIdQuery, Result<PropertyDto>>
{
    private readonly IRepository<Property> _repo;

    public GetPropertyByIdHandler(IRepository<Property> repo) => _repo = repo;

    public async Task<Result<PropertyDto>> Handle(GetPropertyByIdQuery request, CancellationToken ct)
    {
        var property = await _repo.GetByIdAsync(request.Id, ct);
        if (property is null || property.IsDeleted)
            return Result<PropertyDto>.Failure("Об'єкт нерухомості не знайдено");

        var dto = new PropertyDto(
            property.Id, property.Name, property.Address, property.City,
            property.Type.ToString(), property.Status.ToString(),
            property.TotalArea, property.FloorsCount, property.Description,
            property.OwnerId, property.Owner?.FullName ?? "",
            property.Units?.Count ?? 0,
            property.Units?.Count(u => u.Status == UnitStatus.Occupied) ?? 0,
            property.CreatedAt);

        return Result<PropertyDto>.Success(dto);
    }
}

public class GetUnitsByPropertyHandler : IRequestHandler<GetUnitsByPropertyQuery, IEnumerable<UnitDto>>
{
    private readonly IRepository<Unit> _unitRepo;

    public GetUnitsByPropertyHandler(IRepository<Unit> unitRepo) => _unitRepo = unitRepo;

    public async Task<IEnumerable<UnitDto>> Handle(GetUnitsByPropertyQuery request, CancellationToken ct)
    {
        var allUnits = await _unitRepo.GetAllAsync(ct);

        return allUnits
            .Where(u => u.PropertyId == request.PropertyId && !u.IsDeleted)
            .Select(u => new UnitDto(
                u.Id,
                u.Number,
                u.Floor,
                u.Area,
                u.RoomsCount,
                u.BaseRentPrice,
                u.Status.ToString(),
                u.PropertyId,
                u.Property?.Name ?? ""
            ))
            .ToList();
    }
}

public class CreateUnitHandler : IRequestHandler<CreateUnitCommand, Result<UnitDto>>
{
    private readonly IRepository<Unit> _unitRepo;
    private readonly IRepository<Property> _propertyRepo;
    private readonly IUnitOfWork _uow;

    public CreateUnitHandler(IRepository<Unit> unitRepo, IRepository<Property> propertyRepo, IUnitOfWork uow)
    {
        _unitRepo = unitRepo;
        _propertyRepo = propertyRepo;
        _uow = uow;
    }

    public async Task<Result<UnitDto>> Handle(CreateUnitCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;

        var property = await _propertyRepo.GetByIdAsync(d.PropertyId, ct);
        if (property is null || property.IsDeleted)
            return Result<UnitDto>.Failure("Об'єкт нерухомості не знайдено");

        var unit = new Unit
        {
            Number = d.Number,
            Floor = d.Floor,
            Area = d.Area,
            RoomsCount = d.RoomsCount,
            BaseRentPrice = d.BaseRentPrice,
            PropertyId = d.PropertyId,
            Status = UnitStatus.Available,
            Description = d.Description
        };

        await _unitRepo.AddAsync(unit, ct);
        await _uow.SaveChangesAsync(ct);

        var dto = new UnitDto(
            unit.Id,
            unit.Number,
            unit.Floor,
            unit.Area,
            unit.RoomsCount,
            unit.BaseRentPrice,
            unit.Status.ToString(),
            unit.PropertyId,
            property.Name
        );

        return Result<UnitDto>.Success(dto);
    }
}