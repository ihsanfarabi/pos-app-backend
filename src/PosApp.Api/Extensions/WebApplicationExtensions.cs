using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using PosApp.Api.Endpoints;
using PosApp.Infrastructure.Persistence;

namespace PosApp.Api.Extensions;

public static class WebApplicationExtensions
{
    public static Task InitialiseDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        return DatabaseInitializer.InitialiseAsync(app.Services, app.Configuration, cancellationToken);
    }

    public static WebApplication UsePosAppForwardedHeaders(this WebApplication app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
            KnownNetworks = { },
            KnownProxies = { }
        });

        return app;
    }

    public static WebApplication UsePosAppSwaggerUI(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.UseSwagger();
        app.UseSwaggerUI();
        return app;
    }

    public static WebApplication MapPosAppRootEndpoint(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/", () => Results.Redirect("/swagger"));
        }
        else
        {
            app.MapGet("/", () => Results.Ok(new { status = "ok" }));
        }

        return app;
    }

        public static WebApplication UseTraceIdHeader(this WebApplication app)
        {
            app.Use(async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    var traceId = context.TraceIdentifier;
                    if (!string.IsNullOrWhiteSpace(traceId))
                    {
                        context.Response.Headers["x-trace-id"] = traceId;
                    }
                    return Task.CompletedTask;
                });
                await next();
            });

            return app;
        }

    public static WebApplication MapPosAppEndpoints(this WebApplication app)
    {
        app.MapMenuEndpoints();
        app.MapTicketEndpoints();
        app.MapAuthEndpoints();
        return app;
    }
}
