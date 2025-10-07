using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using PosApp.Application.Abstractions.Security;
using PosApp.Application.Contracts;
using PosApp.Application.Features.Auth.Commands;
using PosApp.Api.Services;

namespace PosApp.Api.Endpoints;

public static class AuthEndpoints
{
    private const string RefreshTokenCookieName = "refresh_token";

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var versionedApi = routes.NewVersionedApi("Auth");
        var group = versionedApi
            .MapGroup("/api/auth")
            .HasApiVersion(1, 0)
            .WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("RegisterUser")
            .WithSummary("Register user")
            .WithDescription("Register a new user account.")
            .Produces<UserRegisteredResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/login", LoginAsync)
            .WithName("LoginUser")
            .WithSummary("Authenticate user")
            .WithDescription("Authenticate a user with email and password.")
            .Produces<AuthTokensResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/me", MeAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get current user")
            .WithDescription("Retrieve details about the authenticated user.")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshTokens")
            .WithSummary("Refresh access token")
            .WithDescription("Exchange a refresh token for a new access token.")
            .Produces<AuthTokensResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/logout", Logout)
            .WithName("LogoutUser")
            .WithSummary("Log out user")
            .WithDescription("Invalidate the refresh token for the current user.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Results<Created<UserRegisteredResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterDto dto,
        [AsParameters] AuthServices services,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        services.Logger.LogInformation("Registering user with email {Email}", normalizedEmail);

        var id = await services.Sender.Send(new RegisterUserCommand(dto), cancellationToken);

        services.Logger.LogInformation("Registered user {UserId} with email {Email}", id, normalizedEmail);
        return TypedResults.Created($"/api/users/{id}", new UserRegisteredResponse(id, normalizedEmail));
    }

    private static async Task<Results<Ok<AuthTokensResponse>, UnauthorizedHttpResult, ProblemHttpResult>> LoginAsync(
        LoginDto dto,
        [AsParameters] AuthServices services,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation("Login attempt for email {Email}", dto.Email);

        var result = await services.Sender.Send(new LoginCommand(dto), cancellationToken);
        if (result is null)
        {
            services.Logger.LogWarning("Login failed for email {Email}", dto.Email);
            return TypedResults.Unauthorized();
        }

        IssueRefreshCookie(httpContext, result.RefreshToken);
        services.Logger.LogInformation("User {Email} authenticated", dto.Email);
        return TypedResults.Ok(new AuthTokensResponse(
            result.AccessToken.Token,
            "Bearer",
            result.AccessToken.ExpiresInSeconds));
    }

    private static Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult> MeAsync(
        ClaimsPrincipal user,
        [AsParameters] AuthServices services)
    {
        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            services.Logger.LogWarning("Attempt to access /me without authentication");
            return TypedResults.Unauthorized();
        }

        var id = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email);
        var role = user.FindFirstValue(ClaimTypes.Role) ?? "user";

        services.Logger.LogInformation("Returning profile for user {UserId}", id);
        return TypedResults.Ok(new CurrentUserResponse(id, email, role));
    }

    private static async Task<Results<Ok<AuthTokensResponse>, UnauthorizedHttpResult, ProblemHttpResult>> RefreshAsync(
        HttpContext httpContext,
        [AsParameters] AuthServices services,
        CancellationToken cancellationToken)
    {
        services.Logger.LogInformation("Refreshing tokens for request");

        if (!httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken))
        {
            services.Logger.LogWarning("Refresh token missing for request");
            return TypedResults.Unauthorized();
        }
        var result = await services.Sender.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
        if (result is null)
        {
            services.Logger.LogWarning("Refresh token invalid");
            return TypedResults.Unauthorized();
        }

        IssueRefreshCookie(httpContext, result.RefreshToken);
        services.Logger.LogInformation("Issued refreshed tokens");
        return TypedResults.Ok(new AuthTokensResponse(
            result.AccessToken.Token,
            "Bearer",
            result.AccessToken.ExpiresInSeconds));
    }

    private static NoContent Logout(
        HttpContext httpContext,
        [AsParameters] AuthServices services)
    {
        var userId = httpContext.User?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        services.Logger.LogInformation("Logging out user {UserId}", userId);

        httpContext.Response.Cookies.Append(RefreshTokenCookieName, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = !IsDevelopment(httpContext),
            SameSite = IsDevelopment(httpContext) ? SameSiteMode.Lax : SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            Path = "/"
        });

        services.Logger.LogInformation("User logged out");
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
