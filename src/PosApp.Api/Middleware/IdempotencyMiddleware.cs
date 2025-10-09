using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PosApp.Application.Abstractions.Idempotency;

namespace PosApp.Api.Middleware;

public sealed class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    private const string HeaderName = "Idempotency-Key";
    private readonly RequestDelegate _next = next;
    private readonly ILogger<IdempotencyMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context, IIdempotencyContext idempotencyContext)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var key = values.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                Guid? userId = null;
                var subject = context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                              ?? context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(subject, out var parsed))
                {
                    userId = parsed;
                }

                idempotencyContext.Set(key, userId);
                _logger.LogDebug("Idempotency context enabled with key {IdempotencyKey} for user {UserId}", key, userId);
            }
            else
            {
                _logger.LogDebug("Received empty {HeaderName} header", HeaderName);
            }
        }

        await _next(context);
    }
}
