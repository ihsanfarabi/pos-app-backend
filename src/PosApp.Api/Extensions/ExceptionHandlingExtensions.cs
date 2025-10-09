using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using PosApp.Domain.Exceptions;

namespace PosApp.Api.Extensions;

public static class ExceptionHandlingExtensions
{
    public static WebApplication UsePosAppExceptionHandler(this WebApplication app)
    {
        var logger = app.Logger;
        var environment = app.Environment;

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }

                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                if (exception is not null)
                {
                    logger.LogError(
                        exception,
                        "Unhandled exception processing {Method} {Path}. TraceId {TraceId}",
                        context.Request.Method,
                        context.Request.Path,
                        context.TraceIdentifier);
                }
                else
                {
                    logger.LogError(
                        "Unhandled error pipeline invoked without exception. TraceId {TraceId}",
                        context.TraceIdentifier);
                }

                var response = context.Response;
                response.Clear();
                response.StatusCode = MapStatusCode(exception);
                response.ContentType = "application/problem+json";

                context.Items["PosApp:ExposeExceptionDetails"] = environment.IsDevelopment();

                var problemDetailsService = context.RequestServices.GetService<IProblemDetailsService>();
                if (problemDetailsService is null)
                {
                    throw new InvalidOperationException("IProblemDetailsService is not registered. Ensure AddProblemDetails() is configured.");
                }

                var problemDetailsContext = new ProblemDetailsContext
                {
                    HttpContext = context,
                    Exception = exception
                };

                await problemDetailsService.WriteAsync(problemDetailsContext);
            });
        });

        return app;

        static int MapStatusCode(Exception? exception)
        {
            return exception switch
            {
                DomainException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError
            };
        }
    }
}
