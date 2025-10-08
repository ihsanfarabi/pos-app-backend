using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi.Models;
using PosApp.Application;
using PosApp.Infrastructure;
using Serilog;

namespace PosApp.Api.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext());

        return builder;
    }

    public static WebApplicationBuilder AddPosAppApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);

        return builder;
    }

    public static WebApplicationBuilder AddPosAppApiServices(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddPosAppProblemDetails()
            .AddPosAppApiVersioning()
            .AddPosAppCors(builder.Configuration)
            .AddPosAppAuthentication(builder.Configuration);

        return builder;
    }

    public static WebApplicationBuilder AddPosAppSwagger(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            return builder;
        }

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "POS API", Version = "v1" });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste JWT only (no 'Bearer ' prefix)"
            };

            c.AddSecurityDefinition("Bearer", securityScheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return builder;
    }
}
