using MediatR;
using RentalManagement.Application.Common;

namespace RentalManagement.Application.Features.Auth;

// ─── DTOs ────────────────────────────────────────────────────

public record LoginDto(string Email, string Password);

public record RegisterDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role);

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string Email,
    string FullName,
    string Role);

public record RefreshTokenDto(string RefreshToken);

// ─── COMMANDS ────────────────────────────────────────────────

public record LoginCommand(string Email, string Password)
    : IRequest<Result<AuthResponseDto>>;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role)
    : IRequest<Result<AuthResponseDto>>;

public record RefreshTokenCommand(string RefreshToken)
    : IRequest<Result<AuthResponseDto>>;

// ─── AUTH SERVICE INTERFACE ───────────────────────────────────

public interface IAuthService
{
    Task<Result<AuthResponseDto>> LoginAsync(LoginCommand command, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> RegisterAsync(RegisterCommand command, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
}

// ─── HANDLERS ────────────────────────────────────────────────

public class LoginHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IAuthService _authService;

    public LoginHandler(IAuthService authService) => _authService = authService;

    public Task<Result<AuthResponseDto>> Handle(LoginCommand request, CancellationToken ct)
        => _authService.LoginAsync(request, ct);
}

public class RegisterHandler : IRequestHandler<RegisterCommand, Result<AuthResponseDto>>
{
    private readonly IAuthService _authService;

    public RegisterHandler(IAuthService authService) => _authService = authService;

    public Task<Result<AuthResponseDto>> Handle(RegisterCommand request, CancellationToken ct)
        => _authService.RegisterAsync(request, ct);
}

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IAuthService _authService;

    public RefreshTokenHandler(IAuthService authService) => _authService = authService;

    public Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand request, CancellationToken ct)
        => _authService.RefreshTokenAsync(request.RefreshToken, ct);
}
