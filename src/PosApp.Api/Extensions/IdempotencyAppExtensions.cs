using Microsoft.AspNetCore.Builder;
using PosApp.Api.Middleware;

namespace PosApp.Api.Extensions;

public static class IdempotencyAppExtensions
{
    public static WebApplication UseIdempotencyContext(this WebApplication app)
    {
        app.UseMiddleware<IdempotencyMiddleware>();
        return app;
    }
}
