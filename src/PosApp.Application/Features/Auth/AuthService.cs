using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Abstractions.Security;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Domain.Entities;

namespace PosApp.Application.Features.Auth;

public sealed class AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService)
    : IAuthService
{
    public async Task<Guid> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (await userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new ValidationException("Email already registered.", "email");
        }

        var role = string.IsNullOrWhiteSpace(dto.Role) ? "user" : dto.Role.Trim();

        var user = new User
        {
            Email = normalizedEmail,
            Role = role
        };

        user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return user.Id;
    }

    public async Task<LoginResult?> LoginAsync(LoginDto dto, CancellationToken cancellationToken)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = tokenService.CreateRefreshToken(user);
        return new LoginResult(accessToken, refreshToken);
    }

    public async Task<LoginResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var principal = tokenService.ValidateRefreshToken(refreshToken);
        if (principal is null)
        {
            return null;
        }

        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
        {
            return null;
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var accessToken = tokenService.CreateAccessToken(user);
        var refreshTokenNew = tokenService.CreateRefreshToken(user);
        return new LoginResult(accessToken, refreshTokenNew);
    }
}
