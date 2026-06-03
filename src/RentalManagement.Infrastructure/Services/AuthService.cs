using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RentalManagement.Application.Common;
using RentalManagement.Application.Features.Auth;
using RentalManagement.Infrastructure.Identity;

namespace RentalManagement.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public AuthService(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _userManager = userManager;
        _config = config;
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginCommand cmd, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(cmd.Email);
        if (user == null || !user.IsActive)
            return Result<AuthResponseDto>.Failure("Invalid credentials");

        if (!await _userManager.CheckPasswordAsync(user, cmd.Password))
            return Result<AuthResponseDto>.Failure("Invalid credentials");

        return await GenerateAuthResponse(user);
    }

    public async Task<Result<AuthResponseDto>> RegisterAsync(RegisterCommand cmd, CancellationToken ct = default)
    {
        var exists = await _userManager.FindByEmailAsync(cmd.Email);
        if (exists != null)
            return Result<AuthResponseDto>.Failure("Email already registered");

        var user = new ApplicationUser
        {
            Email = cmd.Email,
            UserName = cmd.Email,
            FirstName = cmd.FirstName,
            LastName = cmd.LastName,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, cmd.Password);
        if (!result.Succeeded)
            return Result<AuthResponseDto>.Failure(string.Join(", ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, cmd.Role);
        return await GenerateAuthResponse(user);
    }

    public async Task<Result<AuthResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var user = _userManager.Users
            .SingleOrDefault(u => u.RefreshToken == refreshToken && u.RefreshTokenExpiry > DateTime.UtcNow);

        if (user == null)
            return Result<AuthResponseDto>.Failure("Invalid or expired refresh token");

        return await GenerateAuthResponse(user);
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var user = _userManager.Users.SingleOrDefault(u => u.RefreshToken == refreshToken);
        if (user == null) return;

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _userManager.UpdateAsync(user);
    }

    // ── Private helpers ──────────────────────────────────────

    private async Task<Result<AuthResponseDto>> GenerateAuthResponse(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Viewer";

        var accessToken = GenerateAccessToken(user, role);
        var refreshToken = GenerateRefreshToken();
        var expiry = DateTime.UtcNow.AddMinutes(GetJwtExpiry());

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            accessToken, refreshToken, expiry,
            user.Email!, user.FullName, role));
    }

    private string GenerateAccessToken(ApplicationUser user, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(GetJwtExpiry()),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private int GetJwtExpiry() =>
        int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 60;
}
