using System;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PosApp.Domain.Exceptions;

namespace PosApp.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPosAppProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                if (context.Exception is null)
                {
                    return;
                }

                context.ProblemDetails.Detail ??= context.Exception.Message;

                    switch (context.Exception)
                {
                        case ValidationProblemException vpex:
                            context.ProblemDetails.Status ??= StatusCodes.Status400BadRequest;
                            context.ProblemDetails.Title ??= "One or more validation errors occurred.";
                            context.ProblemDetails.Type ??= "https://httpstatuses.com/400";
                            context.ProblemDetails.Extensions["code"] = "ValidationFailed";
                            var errors = new Dictionary<string, object>();
                            foreach (var kvp in vpex.Errors)
                            {
                                errors[kvp.Key] = kvp.Value.Select(e => new { code = e.Code, message = e.Message }).ToArray();
                            }
                            context.ProblemDetails.Extensions["errors"] = errors;
                            break;
                    case DomainException:
                        context.ProblemDetails.Status ??= StatusCodes.Status400BadRequest;
                            context.ProblemDetails.Title ??= "Bad Request";
                            context.ProblemDetails.Type ??= "https://httpstatuses.com/400";
                        break;
                    case KeyNotFoundException:
                        context.ProblemDetails.Status ??= StatusCodes.Status404NotFound;
                        context.ProblemDetails.Title ??= "Not Found";
                        context.ProblemDetails.Type ??= "https://httpstatuses.com/404";
                        break;
                    default:
                        context.ProblemDetails.Status ??= StatusCodes.Status500InternalServerError;
                        context.ProblemDetails.Title ??= "Internal Server Error";
                        context.ProblemDetails.Type ??= "https://httpstatuses.com/500";
                        break;
                }
            };
        });

        return services;
    }

    public static IServiceCollection AddPosAppApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });

        return services;
    }

    public static IServiceCollection AddPosAppCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOriginsCsv = configuration["AllowedOrigins"];
        var allowedOrigins = string.IsNullOrWhiteSpace(allowedOriginsCsv)
            ? new[] { "http://localhost:3000" }
            : allowedOriginsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials());
        });

        return services;
    }

    public static IServiceCollection AddPosAppAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection["Issuer"] ?? string.Empty;
        var audience = jwtSection["Audience"] ?? string.Empty;
        var signingKey = jwtSection["SigningKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("JWT SigningKey is not configured. Set Jwt__SigningKey env var.");
        }

        var signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
        var key = new SymmetricSecurityKey(signingKeyBytes);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
        });

        return services;
    }
}
