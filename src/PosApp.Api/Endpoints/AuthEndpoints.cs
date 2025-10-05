using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using MediatR;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using PosApp.Api.Extensions;
using PosApp.Application.Abstractions.Security;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Application.Features.Auth.Commands;

namespace PosApp.Api.Endpoints;

public static class AuthEndpoints
{
    private const string RefreshTokenCookieName = "refresh_token";

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth");

        group.MapPost("/register", RegisterAsync);

        group.MapPost("/login", LoginAsync);

        group.MapGet("/me", MeAsync)
            .RequireAuthorization();

        group.MapPost("/refresh", RefreshAsync);

        group.MapPost("/logout", Logout);

        return group;
    }

    private static async Task<Results<Created<UserRegisteredResponse>, ValidationProblem>> RegisterAsync(
        RegisterDto dto,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await sender.Send(new RegisterUserCommand(dto), cancellationToken);
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            return TypedResults.Created($"/api/users/{id}", new UserRegisteredResponse(id, normalizedEmail));
        }
        catch (ValidationException ve)
        {
            return TypedResults.ValidationProblem(ve.Errors.ToDictionary(), statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PosApp.Application.Exceptions.ValidationException ve)
        {
            var key = string.IsNullOrWhiteSpace(ve.PropertyName) ? "error" : char.ToLowerInvariant(ve.PropertyName[0]) + ve.PropertyName[1..];
            var errors = new Dictionary<string, string[]> { [key] = new[] { ve.Message } };
            return TypedResults.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<Results<Ok<AuthTokensResponse>, UnauthorizedHttpResult, ValidationProblem>> LoginAsync(
        LoginDto dto,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(new LoginCommand(dto), cancellationToken);
            if (result is null)
            {
                return TypedResults.Unauthorized();
            }

            IssueRefreshCookie(httpContext, result.RefreshToken);
            return TypedResults.Ok(new AuthTokensResponse(
                result.AccessToken.Token,
                "Bearer",
                result.AccessToken.ExpiresInSeconds));
        }
        catch (ValidationException ve)
        {
            return TypedResults.ValidationProblem(ve.Errors.ToDictionary(), statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PosApp.Application.Exceptions.ValidationException ve)
        {
            var key = string.IsNullOrWhiteSpace(ve.PropertyName) ? "error" : char.ToLowerInvariant(ve.PropertyName[0]) + ve.PropertyName[1..];
            var errors = new Dictionary<string, string[]> { [key] = new[] { ve.Message } };
            return TypedResults.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult> MeAsync(ClaimsPrincipal user)
    {
        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            return TypedResults.Unauthorized();
        }

        var id = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email);
        var role = user.FindFirstValue(ClaimTypes.Role) ?? "user";

        return TypedResults.Ok(new CurrentUserResponse(id, email, role));
    }

    private static async Task<Results<Ok<AuthTokensResponse>, UnauthorizedHttpResult, ValidationProblem>> RefreshAsync(
        HttpContext httpContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken))
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            var result = await sender.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
            if (result is null)
            {
                return TypedResults.Unauthorized();
            }

            IssueRefreshCookie(httpContext, result.RefreshToken);
            return TypedResults.Ok(new AuthTokensResponse(
                result.AccessToken.Token,
                "Bearer",
                result.AccessToken.ExpiresInSeconds));
        }
        catch (ValidationException ve)
        {
            return TypedResults.ValidationProblem(ve.Errors.ToDictionary(), statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PosApp.Application.Exceptions.ValidationException ve)
        {
            var key = string.IsNullOrWhiteSpace(ve.PropertyName) ? "error" : char.ToLowerInvariant(ve.PropertyName[0]) + ve.PropertyName[1..];
            var errors = new Dictionary<string, string[]> { [key] = new[] { ve.Message } };
            return TypedResults.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static NoContent Logout(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Append(RefreshTokenCookieName, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = !IsDevelopment(httpContext),
            SameSite = IsDevelopment(httpContext) ? SameSiteMode.Lax : SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            Path = "/"
        });

        return TypedResults.NoContent();
    }

    private static void IssueRefreshCookie(HttpContext context, RefreshToken refreshToken)
    {
        var isDev = IsDevelopment(context);
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
            Expires = refreshToken.ExpiresAt,
            Path = "/"
        };

        context.Response.Cookies.Append(RefreshTokenCookieName, refreshToken.Token, options);
    }

    private static bool IsDevelopment(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
    }

    

    private sealed record UserRegisteredResponse(Guid Id, string Email);

    private sealed record CurrentUserResponse(string? Id, string? Email, string Role);

    private sealed record AuthTokensResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
