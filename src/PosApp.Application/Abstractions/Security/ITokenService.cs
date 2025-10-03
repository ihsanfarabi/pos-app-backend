using System.Security.Claims;
using PosApp.Domain.Entities;

namespace PosApp.Application.Abstractions.Security;

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    RefreshToken CreateRefreshToken(User user);

    ClaimsPrincipal? ValidateRefreshToken(string refreshToken);
}

public sealed record AccessToken(string Token, int ExpiresInSeconds);

public sealed record RefreshToken(string Token, DateTimeOffset ExpiresAt);
