using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PosApp.Api.Extensions;
using PosApp.Application;
using PosApp.Application.Abstractions.Security;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Application.Features.Auth.Commands;
using PosApp.Application.Features.Menu.Commands;
using PosApp.Application.Features.Menu.Queries;
using PosApp.Application.Features.Tickets.Commands;
using PosApp.Application.Features.Tickets.Queries;
using PosApp.Application.Validation;
using PosApp.Infrastructure;
using PosApp.Infrastructure.Persistence;
using AppValidationException = PosApp.Application.Exceptions.ValidationException;

const string RefreshTokenCookieName = "refresh_token";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();

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

MapMenuEndpoints(app);
MapTicketEndpoints(app);
MapAuthEndpoints(app);

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

static void MapMenuEndpoints(WebApplication app)
{
    app.MapGet("/api/menu", async ([AsParameters] MenuQueryDto query, ISender sender, CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await sender.Send(new GetMenuItemsQuery(query), cancellationToken);
            if (result.Pagination is null)
            {
                return Results.Ok(result.Items);
            }

            return Results.Ok(new
            {
                items = result.Items,
                page = result.Pagination.Page,
                pageSize = result.Pagination.PageSize,
                total = result.Pagination.Total
            });
        }
        catch (AppValidationException ex)
        {
            return ValidationProblem(ex);
        }
    })
    .WithValidator<MenuQueryDto>()
    .RequireAuthorization();

    app.MapPost("/api/menu", async (ISender sender, CreateMenuItemDto dto, CancellationToken cancellationToken) =>
    {
        var id = await sender.Send(new CreateMenuItemCommand(dto), cancellationToken);
        return Results.Created($"/api/menu/{id}", new { id });
    })
    .WithValidator<CreateMenuItemDto>()
    .RequireAuthorization("Admin");

    app.MapPut("/api/menu/{id:guid}", async (Guid id, UpdateMenuItemDto dto, ISender sender, CancellationToken cancellationToken) =>
    {
        try
        {
            await sender.Send(new UpdateMenuItemCommand(id, dto), cancellationToken);
            return Results.Ok(new { id });
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    })
    .WithValidator<UpdateMenuItemDto>()
    .RequireAuthorization("Admin");

    app.MapDelete("/api/menu/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
    {
        var deleted = await sender.Send(new DeleteMenuItemCommand(id), cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .RequireAuthorization("Admin");
}

static void MapTicketEndpoints(WebApplication app)
{
    app.MapPost("/api/tickets", async (ISender sender, CancellationToken cancellationToken) =>
    {
        var id = await sender.Send(new CreateTicketCommand(), cancellationToken);
        return Results.Created($"/api/tickets/{id}", new { id });
    })
    .RequireAuthorization();

    app.MapGet("/api/tickets/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
    {
        var ticket = await sender.Send(new GetTicketDetailsQuery(id), cancellationToken);
        return ticket is null ? Results.NotFound() : Results.Ok(ticket);
    })
    .RequireAuthorization();

    app.MapGet("/api/tickets", async ([AsParameters] TicketListQueryDto query, ISender sender, CancellationToken cancellationToken) =>
    {
        var result = await sender.Send(new GetTicketsQuery(query), cancellationToken);
        return Results.Ok(new
        {
            items = result.Items,
            page = result.Pagination.Page,
            pageSize = result.Pagination.PageSize,
            total = result.Pagination.Total
        });
    })
    .WithValidator<TicketListQueryDto>()
    .RequireAuthorization();

    app.MapPost("/api/tickets/{id:guid}/lines", async (Guid id, AddLineDto dto, ISender sender, CancellationToken cancellationToken) =>
    {
        try
        {
            await sender.Send(new AddTicketLineCommand(id, dto), cancellationToken);
            return Results.Created($"/api/tickets/{id}", new { ok = true });
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (AppValidationException ex)
        {
            return ValidationProblem(ex);
        }
    })
    .WithValidator<AddLineDto>()
    .RequireAuthorization();

    app.MapPost("/api/tickets/{id:guid}/pay/cash", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
    {
        try
        {
            var payment = await sender.Send(new PayTicketCashCommand(id), cancellationToken);
            return Results.Ok(payment);
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (AppValidationException ex)
        {
            return ValidationProblem(ex);
        }
    })
    .RequireAuthorization();
}

static void MapAuthEndpoints(WebApplication app)
{
    app.MapPost("/api/auth/register", async (RegisterDto dto, ISender sender, CancellationToken cancellationToken) =>
    {
        try
        {
            var id = await sender.Send(new RegisterUserCommand(dto), cancellationToken);
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            return Results.Created($"/api/users/{id}", new { id, email = normalizedEmail });
        }
        catch (AppValidationException ex)
        {
            return ValidationProblem(ex);
        }
    })
    .WithValidator<RegisterDto>();

    app.MapPost("/api/auth/login", async (LoginDto dto, ISender sender, HttpContext httpContext, CancellationToken cancellationToken) =>
    {
        var result = await sender.Send(new LoginCommand(dto), cancellationToken);
        if (result is null)
        {
            return Results.Unauthorized();
        }

        IssueRefreshCookie(httpContext, result.RefreshToken);
        return Results.Ok(new
        {
            access_token = result.AccessToken.Token,
            token_type = "Bearer",
            expires_in = result.AccessToken.ExpiresInSeconds
        });
    })
    .WithValidator<LoginDto>();

    app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
    {
        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email);
        var role = user.FindFirstValue(ClaimTypes.Role) ?? "user";
        return Results.Ok(new { id = sub, email, role });
    })
    .RequireAuthorization();

    app.MapPost("/api/auth/refresh", async (HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
    {
        if (!httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
        if (result is null)
        {
            return Results.Unauthorized();
        }

        IssueRefreshCookie(httpContext, result.RefreshToken);
        return Results.Ok(new
        {
            access_token = result.AccessToken.Token,
            token_type = "Bearer",
            expires_in = result.AccessToken.ExpiresInSeconds
        });
    });

    app.MapPost("/api/auth/logout", (HttpContext httpContext) =>
    {
        httpContext.Response.Cookies.Append(RefreshTokenCookieName, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = !IsDevelopment(httpContext),
            SameSite = IsDevelopment(httpContext) ? SameSiteMode.Lax : SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            Path = "/"
        });
        return Results.NoContent();
    });
}

static void IssueRefreshCookie(HttpContext context, RefreshToken refreshToken)
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

static bool IsDevelopment(HttpContext context) => context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();

static IResult ValidationProblem(AppValidationException exception)
{
    var key = string.IsNullOrWhiteSpace(exception.PropertyName)
        ? "error"
        : char.ToLowerInvariant(exception.PropertyName[0]) + exception.PropertyName[1..];

    var payload = new Dictionary<string, string[]>
    {
        [key] = new[] { exception.Message }
    };

    return Results.ValidationProblem(payload, statusCode: StatusCodes.Status400BadRequest);
}
