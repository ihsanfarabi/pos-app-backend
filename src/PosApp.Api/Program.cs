using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PosApp.Api.Endpoints;
using PosApp.Application;
using PosApp.Application.Validation;
using PosApp.Infrastructure;
using PosApp.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();

var allowedOriginsCsv = builder.Configuration["AllowedOrigins"];
var allowedOrigins = string.IsNullOrWhiteSpace(allowedOriginsCsv)
    ? new[] { "http://localhost:3000" }
    : allowedOriginsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

ConfigureAuthentication(builder);
ConfigureSwagger(builder);

var app = builder.Build();

await DatabaseInitializer.InitialiseAsync(app.Services, app.Configuration);

app.UseExceptionHandler(handlerApp =>
{
    handlerApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        var problem = new ProblemDetails
        {
            Title = "An error occurred while processing your request."
        };

        switch (exception)
        {
            case FluentValidation.ValidationException fv:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                problem.Status = StatusCodes.Status400BadRequest;
                problem.Title = "Validation failed";
                problem.Extensions["errors"] = fv.Errors
                    .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName ?? "error"))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                break;
            case PosApp.Application.Exceptions.ValidationException ve:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                problem.Status = StatusCodes.Status400BadRequest;
                problem.Title = "Validation failed";
                problem.Extensions["errors"] = new Dictionary<string, string[]>
                {
                    [string.IsNullOrWhiteSpace(ve.PropertyName)
                        ? "error"
                        : char.ToLowerInvariant(ve.PropertyName[0]) + ve.PropertyName[1..]] = new[] { ve.Message }
                };
                break;
            case PosApp.Application.Exceptions.NotFoundException nf:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                problem.Status = StatusCodes.Status404NotFound;
                problem.Title = "Resource not found";
                problem.Detail = nf.Message;
                break;
            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                problem.Status = StatusCodes.Status500InternalServerError;
                problem.Detail = exception?.Message;
                break;
        }

        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    KnownNetworks = { },
    KnownProxies = { }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger"));
}
else
{
    app.MapGet("/", () => Results.Ok(new { status = "ok" }));
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapMenuEndpoints();
app.MapTicketEndpoints();
app.MapAuthEndpoints();

app.Run();

static void ConfigureAuthentication(WebApplicationBuilder builder)
{
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var issuer = jwtSection["Issuer"] ?? string.Empty;
    var audience = jwtSection["Audience"] ?? string.Empty;
    var signingKey = jwtSection["SigningKey"] ?? string.Empty;
    if (string.IsNullOrWhiteSpace(signingKey))
    {
        throw new InvalidOperationException("JWT SigningKey is not configured. Set Jwt__SigningKey env var.");
    }

    var signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
    var key = new SymmetricSecurityKey(signingKeyBytes);

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
    });
}

static void ConfigureSwagger(WebApplicationBuilder builder)
{
    if (!builder.Environment.IsDevelopment())
    {
        return;
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
}
