using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PosApp.Application.Abstractions.Security;
using PosApp.Domain.Entities;
using PosApp.Infrastructure.Options;

namespace PosApp.Infrastructure.Security;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly RefreshJwtOptions _refreshOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public TokenService(IOptions<JwtOptions> jwtOptions, IOptions<RefreshJwtOptions> refreshOptions)
    {
        _jwtOptions = jwtOptions.Value;
        _refreshOptions = refreshOptions.Value;
    }

    public AccessToken CreateAccessToken(User user)
    {
        EnsureAccessTokenConfigured();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_jwtOptions.AccessTokenTtlMinutes);

        var claims = BuildUserClaims(user);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var jwt = _tokenHandler.WriteToken(token);
        var ttlSeconds = (int)(_jwtOptions.AccessTokenTtlMinutes * 60);
        return new AccessToken(jwt, ttlSeconds);
    }

    public RefreshToken CreateRefreshToken(User user)
    {
        EnsureRefreshTokenConfigured();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_refreshOptions.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var expires = now.AddDays(_refreshOptions.TtlDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("typ", "refresh")
        };

        var token = new JwtSecurityToken(
            issuer: _refreshOptions.Issuer,
            audience: _refreshOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var jwt = _tokenHandler.WriteToken(token);
        return new RefreshToken(jwt, expires);
    }

    public ClaimsPrincipal? ValidateRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        EnsureRefreshTokenConfigured();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _refreshOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _refreshOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_refreshOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try
        {
            var principal = _tokenHandler.ValidateToken(refreshToken, validationParameters, out _);
            var type = principal.FindFirstValue("typ");
            if (!string.Equals(type, "refresh", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    private static List<Claim> BuildUserClaims(User user)
    {
        return new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
    }

    private void EnsureAccessTokenConfigured()
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("JWT SigningKey is not configured. Set Jwt:SigningKey.");
        }
    }

    private void EnsureRefreshTokenConfigured()
    {
        if (string.IsNullOrWhiteSpace(_refreshOptions.SigningKey))
        {
            throw new InvalidOperationException("Refresh JWT SigningKey is not configured. Set RefreshJwt:SigningKey.");
        }
    }
}
