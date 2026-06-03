using FluentValidation;
using MediatR;
using RentalManagement.Application.Common;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Interfaces;
using Unit = RentalManagement.Domain.Entities.Unit;

namespace RentalManagement.Application.Features.Meters;

// ─── DTOs ────────────────────────────────────────────────────

public record MeterDto(
    Guid Id,
    string Number,
    string Type,
    Guid UnitId,
    string UnitNumber,
    string PropertyName,
    decimal CurrentReading,
    DateTime LastReadingDate,
    decimal RatePerUnit);

public record MeterReadingDto(
    Guid Id,
    Guid MeterId,
    decimal Reading,
    DateTime ReadingDate,
    decimal Consumption,
    decimal Amount,
    string? Notes);

public record CreateMeterDto(
    string Number,
    string Type,
    Guid UnitId,
    decimal RatePerUnit,
    decimal CurrentReading);

public record AddReadingDto(
    decimal Reading,
    DateTime ReadingDate,
    string? Notes);

// ─── QUERIES ─────────────────────────────────────────────────

public record GetMetersQuery : IRequest<IEnumerable<MeterDto>>;
public record GetMeterByIdQuery(Guid Id) : IRequest<Result<MeterDto>>;
public record GetMeterReadingsQuery(Guid MeterId) : IRequest<IEnumerable<MeterReadingDto>>;

// ─── COMMANDS ────────────────────────────────────────────────

public record CreateMeterCommand(CreateMeterDto Data) : IRequest<Result<MeterDto>>;
public record AddReadingCommand(Guid MeterId, AddReadingDto Data) : IRequest<Result<MeterReadingDto>>;
public record DeleteMeterCommand(Guid Id) : IRequest<Result>;

// ─── VALIDATORS ──────────────────────────────────────────────

public class CreateMeterValidator : AbstractValidator<CreateMeterCommand>
{
    private static readonly string[] ValidTypes = ["electricity", "water_cold", "water_hot", "gas", "heating"];

    public CreateMeterValidator()
    {
        RuleFor(x => x.Data.Number).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Data.Type).NotEmpty().Must(t => ValidTypes.Contains(t))
            .WithMessage("Невірний тип лічильника");
        RuleFor(x => x.Data.UnitId).NotEmpty();
        RuleFor(x => x.Data.RatePerUnit).GreaterThan(0);
        RuleFor(x => x.Data.CurrentReading).GreaterThanOrEqualTo(0);
    }
}

public class AddReadingValidator : AbstractValidator<AddReadingCommand>
{
    public AddReadingValidator()
    {
        RuleFor(x => x.Data.Reading).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Data.ReadingDate).NotEmpty();
    }
}

// ─── HANDLERS ────────────────────────────────────────────────

public class GetMetersHandler : IRequestHandler<GetMetersQuery, IEnumerable<MeterDto>>
{
    private readonly IRepository<Meter> _meters;
    private readonly IRepository<Unit> _units;
    private readonly IRepository<Property> _props;

    public GetMetersHandler(IRepository<Meter> meters, IRepository<Unit> units, IRepository<Property> props)
    { _meters = meters; _units = units; _props = props; }

    public async Task<IEnumerable<MeterDto>> Handle(GetMetersQuery request, CancellationToken ct)
    {
        var meters = await _meters.FindAsync(m => !m.IsDeleted, ct);
        var units = await _units.GetAllAsync(ct);
        var props = await _props.GetAllAsync(ct);

        var unitsDict = units.ToDictionary(u => u.Id);
        var propsDict = props.ToDictionary(p => p.Id);

        return meters.Select(m => {
            unitsDict.TryGetValue(m.UnitId, out var unit);
            Property? prop = null;
            if (unit != null) propsDict.TryGetValue(unit.PropertyId, out prop);
            return new MeterDto(m.Id, m.Number, m.Type, m.UnitId,
                unit?.Number ?? "", prop?.Name ?? "",
                m.CurrentReading, m.LastReadingDate, m.RatePerUnit);
        });
    }
}

public class CreateMeterHandler : IRequestHandler<CreateMeterCommand, Result<MeterDto>>
{
    private readonly IRepository<Meter> _meters;
    private readonly IRepository<Unit> _units;
    private readonly IUnitOfWork _uow;

    public CreateMeterHandler(IRepository<Meter> meters, IRepository<Unit> units, IUnitOfWork uow)
    { _meters = meters; _units = units; _uow = uow; }

    public async Task<Result<MeterDto>> Handle(CreateMeterCommand cmd, CancellationToken ct)
    {
        var unit = await _units.GetByIdAsync(cmd.Data.UnitId, ct);
        if (unit == null) return Result<MeterDto>.Failure("Приміщення не знайдено");

        // Check duplicate number
        var existing = await _meters.FindAsync(m => m.Number == cmd.Data.Number && !m.IsDeleted, ct);
        if (existing.Any()) return Result<MeterDto>.Failure("Лічильник з таким номером вже існує");

        var meter = new Meter
        {
            Number = cmd.Data.Number,
            Type = cmd.Data.Type,
            UnitId = cmd.Data.UnitId,
            RatePerUnit = cmd.Data.RatePerUnit,
            CurrentReading = cmd.Data.CurrentReading,
            LastReadingDate = DateTime.UtcNow
        };
        await _meters.AddAsync(meter, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<MeterDto>.Success(new MeterDto(
            meter.Id, meter.Number, meter.Type, meter.UnitId,
            unit.Number, "", meter.CurrentReading, meter.LastReadingDate, meter.RatePerUnit));
    }
}

public class AddReadingHandler : IRequestHandler<AddReadingCommand, Result<MeterReadingDto>>
{
    private readonly IRepository<Meter> _meters;
    private readonly IRepository<MeterReading> _readings;
    private readonly IUnitOfWork _uow;

    public AddReadingHandler(IRepository<Meter> meters, IRepository<MeterReading> readings, IUnitOfWork uow)
    { _meters = meters; _readings = readings; _uow = uow; }

    public async Task<Result<MeterReadingDto>> Handle(AddReadingCommand cmd, CancellationToken ct)
    {
        var meter = await _meters.GetByIdAsync(cmd.MeterId, ct);
        if (meter == null || meter.IsDeleted) return Result<MeterReadingDto>.Failure("Лічильник не знайдено");

        if (cmd.Data.Reading < meter.CurrentReading)
            return Result<MeterReadingDto>.Failure($"Нові показники ({cmd.Data.Reading}) не можуть бути менші за попередні ({meter.CurrentReading})");

        var consumption = cmd.Data.Reading - meter.CurrentReading;
        var amount = consumption * meter.RatePerUnit;

        var reading = new MeterReading
        {
            MeterId = cmd.MeterId,
            Reading = cmd.Data.Reading,
            ReadingDate = cmd.Data.ReadingDate,
            Consumption = consumption,
            Amount = amount,
            Notes = cmd.Data.Notes
        };
        await _readings.AddAsync(reading, ct);

        meter.CurrentReading = cmd.Data.Reading;
        meter.LastReadingDate = cmd.Data.ReadingDate;
        meter.UpdatedAt = DateTime.UtcNow;
        _meters.Update(meter);

        await _uow.SaveChangesAsync(ct);

        return Result<MeterReadingDto>.Success(new MeterReadingDto(
            reading.Id, reading.MeterId, reading.Reading, reading.ReadingDate,
            reading.Consumption, reading.Amount, reading.Notes));
    }
}

public class DeleteMeterHandler : IRequestHandler<DeleteMeterCommand, Result>
{
    private readonly IRepository<Meter> _meters;
    private readonly IUnitOfWork _uow;

    public DeleteMeterHandler(IRepository<Meter> meters, IUnitOfWork uow) { _meters = meters; _uow = uow; }

    public async Task<Result> Handle(DeleteMeterCommand cmd, CancellationToken ct)
    {
        var meter = await _meters.GetByIdAsync(cmd.Id, ct);
        if (meter == null) return Result.Failure("Лічильник не знайдено");
        meter.IsDeleted = true;
        _meters.Update(meter);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
