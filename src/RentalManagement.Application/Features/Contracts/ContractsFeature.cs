using FluentValidation;
using MediatR;
using RentalManagement.Application.Common;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Domain.Interfaces;
using Unit = RentalManagement.Domain.Entities.Unit;

namespace RentalManagement.Application.Features.Contracts;

// ─── DTOs ────────────────────────────────────────────────────

public record ContractDto(
    Guid Id, string Number,
    Guid UnitId, string UnitNumber, string PropertyName, string PropertyAddress,
    Guid TenantId, string TenantName, string TenantPhone, string TenantEmail,
    DateTime StartDate, DateTime EndDate, decimal MonthlyRent, decimal SecurityDeposit,
    int PaymentDayOfMonth, string Status, string? TerminationReason, DateTime? TerminatedAt,
    string? Notes, int DaysUntilExpiry, decimal TotalDebt, DateTime CreatedAt);

public record ContractSummaryDto(
    Guid Id, string Number, string TenantName,
    string UnitNumber, string PropertyName,
    DateTime StartDate, DateTime EndDate, decimal MonthlyRent,
    string Status, int DaysUntilExpiry);

public record CreateContractDto(
    Guid UnitId, Guid TenantId, DateTime StartDate, DateTime EndDate,
    decimal MonthlyRent, decimal SecurityDeposit, int PaymentDayOfMonth, string? Notes);

public record UpdateContractDto(
    DateTime StartDate, DateTime EndDate, decimal MonthlyRent,
    decimal SecurityDeposit, int PaymentDayOfMonth, string? Notes);

// ─── QUERIES ─────────────────────────────────────────────────

public record GetContractsQuery(
    int Page = 1, int PageSize = 20,
    ContractStatus? Status = null, Guid? TenantId = null,
    Guid? PropertyId = null, bool? ExpiringWithin30Days = null)
    : IRequest<PagedResult<ContractSummaryDto>>;

public record GetContractByIdQuery(Guid Id) : IRequest<Result<ContractDto>>;
public record GetContractsByTenantQuery(Guid TenantId) : IRequest<IEnumerable<ContractSummaryDto>>;

// ─── COMMANDS ────────────────────────────────────────────────

public record CreateContractCommand(CreateContractDto Data) : IRequest<Result<ContractDto>>;
public record UpdateContractCommand(Guid Id, UpdateContractDto Data) : IRequest<Result<ContractDto>>;
public record ActivateContractCommand(Guid Id) : IRequest<Result<ContractDto>>;
public record TerminateContractCommand(Guid Id, string Reason) : IRequest<Result>;
public record SuspendContractCommand(Guid Id) : IRequest<Result>;
public record RenewContractCommand(Guid Id, DateTime NewEndDate, decimal? NewMonthlyRent) : IRequest<Result<ContractDto>>;

// ─── VALIDATORS ──────────────────────────────────────────────

public class CreateContractValidator : AbstractValidator<CreateContractCommand>
{
    public CreateContractValidator()
    {
        RuleFor(x => x.Data.UnitId).NotEmpty();
        RuleFor(x => x.Data.TenantId).NotEmpty();
        RuleFor(x => x.Data.StartDate).NotEmpty();
        RuleFor(x => x.Data.EndDate)
            .NotEmpty()
            .GreaterThan(x => x.Data.StartDate)
            .WithMessage("Дата закінчення має бути після дати початку");
        RuleFor(x => x.Data.MonthlyRent).GreaterThan(0);
        RuleFor(x => x.Data.SecurityDeposit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Data.PaymentDayOfMonth).InclusiveBetween(1, 28);
    }
}

// ─── INTERFACE ───────────────────────────────────────────────

public interface IContractNumberService
{
    Task<string> GenerateNumberAsync(CancellationToken ct = default);
}

public interface IContractNumberGenerator
{
    Task<string> GenerateNumberAsync(CancellationToken ct = default);
    Task<string> GenerateAsync(CancellationToken ct = default);
}

// ─── HELPERS ─────────────────────────────────────────────────

public static class ContractMapper
{
    public static ContractDto ToFullDto(Contract c)
    {
        var debt = c.Invoices
            .Where(i => i.Status is PaymentStatus.Pending or PaymentStatus.Overdue or PaymentStatus.PartiallyPaid)
            .Sum(i => i.DebtAmount);

        return new ContractDto(
            c.Id, c.Number,
            c.UnitId, c.Unit?.Number ?? "", c.Unit?.Property?.Name ?? "", c.Unit?.Property?.Address ?? "",
            c.TenantId, c.Tenant?.FullName ?? "", c.Tenant?.Phone ?? "", c.Tenant?.Email ?? "",
            c.StartDate, c.EndDate, c.MonthlyRent, c.SecurityDeposit, c.PaymentDayOfMonth,
            c.Status.ToString(), c.TerminationReason, c.TerminatedAt, c.Notes,
            (int)(c.EndDate - DateTime.UtcNow).TotalDays, debt, c.CreatedAt);
    }

    public static ContractSummaryDto ToSummary(Contract c) => new(
        c.Id, c.Number, c.Tenant?.FullName ?? "",
        c.Unit?.Number ?? "", c.Unit?.Property?.Name ?? "",
        c.StartDate, c.EndDate, c.MonthlyRent,
        c.Status.ToString(), (int)(c.EndDate - DateTime.UtcNow).TotalDays);
}

// ─── HANDLERS ────────────────────────────────────────────────

public class GetContractsHandler : IRequestHandler<GetContractsQuery, PagedResult<ContractSummaryDto>>
{
    private readonly IRepository<Contract> _repo;
    public GetContractsHandler(IRepository<Contract> repo) => _repo = repo;

    public async Task<PagedResult<ContractSummaryDto>> Handle(GetContractsQuery q, CancellationToken ct)
    {
        var all = await _repo.GetAllAsync(ct);
        var filtered = all.Where(c => !c.IsDeleted);

        if (q.Status.HasValue)        filtered = filtered.Where(c => c.Status == q.Status.Value);
        if (q.TenantId.HasValue)      filtered = filtered.Where(c => c.TenantId == q.TenantId.Value);
        if (q.ExpiringWithin30Days == true)
            filtered = filtered.Where(c =>
                c.Status == ContractStatus.Active &&
                (c.EndDate - DateTime.UtcNow).TotalDays is >= 0 and <= 30);

        var total = filtered.Count();
        var items = filtered
            .OrderByDescending(c => c.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(ContractMapper.ToSummary)
            .ToList();

        return new PagedResult<ContractSummaryDto> { Items = items, TotalCount = total, Page = q.Page, PageSize = q.PageSize };
    }
}

public class GetContractByIdHandler : IRequestHandler<GetContractByIdQuery, Result<ContractDto>>
{
    private readonly IRepository<Contract> _repo;
    public GetContractByIdHandler(IRepository<Contract> repo) => _repo = repo;

    public async Task<Result<ContractDto>> Handle(GetContractByIdQuery q, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(q.Id, ct);
        if (c is null || c.IsDeleted) return Result<ContractDto>.Failure("Contract not found");
        return Result<ContractDto>.Success(ContractMapper.ToFullDto(c));
    }
}

public class CreateContractHandler : IRequestHandler<CreateContractCommand, Result<ContractDto>>
{
    private readonly IRepository<Contract> _contractRepo;
    private readonly IRepository<Unit> _unitRepo;
    private readonly IContractNumberGenerator _numberGen;
    private readonly IUnitOfWork _uow;

    public CreateContractHandler(IRepository<Contract> contractRepo, IRepository<Unit> unitRepo,
        IContractNumberGenerator numberGen, IUnitOfWork uow)
    { _contractRepo = contractRepo; _unitRepo = unitRepo; _numberGen = numberGen; _uow = uow; }

    public async Task<Result<ContractDto>> Handle(CreateContractCommand cmd, CancellationToken ct)
    {
        var d = cmd.Data;
        var unit = await _unitRepo.GetByIdAsync(d.UnitId, ct);
        if (unit is null) return Result<ContractDto>.Failure("Unit not found");
        if (unit.Status == UnitStatus.Occupied)
            return Result<ContractDto>.Failure("Unit is already occupied");

        var contract = new Contract
        {
            Number = await _numberGen.GenerateAsync(ct),
            UnitId = d.UnitId, TenantId = d.TenantId,
            StartDate = d.StartDate, EndDate = d.EndDate,
            MonthlyRent = d.MonthlyRent, SecurityDeposit = d.SecurityDeposit,
            PaymentDayOfMonth = d.PaymentDayOfMonth, Notes = d.Notes,
            Status = ContractStatus.Draft
        };

        await _contractRepo.AddAsync(contract, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ContractDto>.Success(ContractMapper.ToFullDto(contract));
    }
}

public class ActivateContractHandler : IRequestHandler<ActivateContractCommand, Result<ContractDto>>
{
    private readonly IRepository<Contract> _contractRepo;
    private readonly IRepository<Unit> _unitRepo;
    private readonly IUnitOfWork _uow;

    public ActivateContractHandler(IRepository<Contract> c, IRepository<Unit> u, IUnitOfWork uow)
    { _contractRepo = c; _unitRepo = u; _uow = uow; }

    public async Task<Result<ContractDto>> Handle(ActivateContractCommand cmd, CancellationToken ct)
    {
        var contract = await _contractRepo.GetByIdAsync(cmd.Id, ct);
        if (contract is null) return Result<ContractDto>.Failure("Contract not found");
        if (contract.Status != ContractStatus.Draft)
            return Result<ContractDto>.Failure($"Cannot activate contract with status '{contract.Status}'");

        var unit = await _unitRepo.GetByIdAsync(contract.UnitId, ct);
        if (unit != null) { unit.Status = UnitStatus.Occupied; _unitRepo.Update(unit); }

        contract.Status = ContractStatus.Active;
        _contractRepo.Update(contract);
        await _uow.SaveChangesAsync(ct);
        return Result<ContractDto>.Success(ContractMapper.ToFullDto(contract));
    }
}

public class TerminateContractHandler : IRequestHandler<TerminateContractCommand, Result>
{
    private readonly IRepository<Contract> _contractRepo;
    private readonly IRepository<Unit> _unitRepo;
    private readonly IUnitOfWork _uow;

    public TerminateContractHandler(IRepository<Contract> c, IRepository<Unit> u, IUnitOfWork uow)
    { _contractRepo = c; _unitRepo = u; _uow = uow; }

    public async Task<Result> Handle(TerminateContractCommand cmd, CancellationToken ct)
    {
        var contract = await _contractRepo.GetByIdAsync(cmd.Id, ct);
        if (contract is null) return Result.Failure("Contract not found");
        if (contract.Status is ContractStatus.Terminated or ContractStatus.Expired)
            return Result.Failure("Contract is already closed");

        var unit = await _unitRepo.GetByIdAsync(contract.UnitId, ct);
        if (unit != null) { unit.Status = UnitStatus.Available; _unitRepo.Update(unit); }

        contract.Status = ContractStatus.Terminated;
        contract.TerminationReason = cmd.Reason;
        contract.TerminatedAt = DateTime.UtcNow;
        _contractRepo.Update(contract);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
