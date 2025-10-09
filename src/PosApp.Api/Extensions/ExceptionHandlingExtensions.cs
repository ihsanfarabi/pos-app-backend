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
                    logger.LogError(exception, "Unhandled exception {TraceId}", context.TraceIdentifier);
                }

                var response = context.Response;
                response.Clear();
                response.StatusCode = MapStatusCode(exception);
                response.ContentType = "application/problem+json";

                var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
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
