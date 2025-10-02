using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Identity;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using PosApi.Contracts;
using PosApi.Domain;
using PosApi.Infrastructure;
using PosApi.Validation;

var builder = WebApplication.CreateBuilder(args);

// Db
var connectionString = builder.Configuration.GetConnectionString("Default") ?? string.Empty;
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(connectionString);
});

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// CORS
var allowedOriginsCsv = builder.Configuration["AllowedOrigins"];
var allowedOrigins = string.IsNullOrWhiteSpace(allowedOriginsCsv)
    ? new[] { "http://localhost:3000" }
    : allowedOriginsCsv.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// Auth
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtIssuer = jwtSection["Issuer"] ?? string.Empty;
var jwtAudience = jwtSection["Audience"] ?? string.Empty;
var jwtSigningKey = jwtSection["SigningKey"] ?? string.Empty;
if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    throw new InvalidOperationException("JWT SigningKey is not configured. Set Jwt__SigningKey env var.");
}

if (!string.IsNullOrWhiteSpace(jwtSigningKey))
{
    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
    });
}

// Password hasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Swagger (dev only)
if (builder.Environment.IsDevelopment())
{
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

var app = builder.Build();

// Auto-migrate + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    db.Database.Migrate();

    if (!db.Menu.Any())
    {
        db.Menu.AddRange(
            new MenuItem { Name = "Nasi Goreng Ati Ampela", Price = 25000 },
            new MenuItem { Name = "Mie Goreng",  Price = 22000 },
            new MenuItem { Name = "Teh Manis",   Price = 8000  }
        );
        db.SaveChanges();
    }

    // Seed admin user if configured
    var adminEmail = builder.Configuration["Admin:Email"] ?? "admin@example.com";
    var adminPassword = builder.Configuration["Admin:Password"] ?? "ChangeMe123!";
    if (!await db.Users.AnyAsync(u => u.Email == adminEmail.ToLower()))
    {
        var admin = new User { Email = adminEmail.ToLower(), Role = "admin" };
        admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}

// Honor proxy headers (HTTPS offload)
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

// Enable CORS for frontend dev server
app.UseCors();

// AuthZ
app.UseAuthentication();
app.UseAuthorization();

// MENU
app.MapGet("/api/menu", async (MenuQueryDto query, AppDbContext db) =>
{
    var baseQuery = db.Menu.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(query.Q))
    {
        var term = query.Q.Trim().ToLower();
        baseQuery = baseQuery.Where(m => EF.Functions.Like(m.Name.ToLower(), $"%{term}%"));
    }

    baseQuery = baseQuery.OrderBy(m => m.Name);

    var paginationRequested = query.Page.HasValue || query.PageSize.HasValue;
    if (!paginationRequested)
    {
        var list = await baseQuery.Select(m => new { m.Id, m.Name, m.Price }).ToListAsync();
        return Results.Ok(list);
    }

    var page = query.Page ?? 1;
    var pageSize = query.PageSize ?? 20;
    pageSize = Math.Min(pageSize, 100);

    var total = await baseQuery.CountAsync();
    var items = await baseQuery
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(m => new { m.Id, m.Name, m.Price })
        .ToListAsync();

    return Results.Ok(new { items, page, pageSize, total });
})
.WithValidator<MenuQueryDto>()
.RequireAuthorization();

app.MapPost("/api/menu", async (AppDbContext db, CreateMenuItemDto dto) =>
{
    var item = new MenuItem { Name = dto.Name.Trim(), Price = dto.Price };
    db.Menu.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/menu/{item.Id}", new { item.Id });
})
.WithValidator<CreateMenuItemDto>()
.RequireAuthorization("Admin");

app.MapPut("/api/menu/{id:guid}", async (Guid id, UpdateMenuItemDto dto, AppDbContext db) =>
{
    var item = await db.Menu.FindAsync(id);
    if (item is null) return Results.NotFound();

    item.Name = dto.Name.Trim();
    item.Price = dto.Price;
    await db.SaveChangesAsync();
    return Results.Ok(new { item.Id });
})
.WithValidator<UpdateMenuItemDto>()
.RequireAuthorization("Admin");

app.MapDelete("/api/menu/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var item = await db.Menu.FindAsync(id);
    if (item is null) return Results.NotFound();
    db.Menu.Remove(item);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("Admin");

// TICKETS
app.MapPost("/api/tickets", async (AppDbContext db) =>
{
    var t = new Ticket();
    db.Tickets.Add(t);
    await db.SaveChangesAsync();
    return Results.Created($"/api/tickets/{t.Id}", new { t.Id });
}).RequireAuthorization();

app.MapGet("/api/tickets/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var t = await db.Tickets.AsNoTracking()
        .Include(x => x.Lines)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (t is null) return Results.NotFound();

    var lines = await db.TicketLines
        .Where(l => l.TicketId == id)
        .Join(db.Menu, l => l.MenuItemId, m => m.Id, (l, m) => new
        {
            l.Id,
            ItemName = m.Name,
            l.Qty,
            l.UnitPrice,
            LineTotal = l.Qty * l.UnitPrice
        })
        .ToListAsync();

    var total = lines.Sum(x => x.LineTotal);
    return Results.Ok(new { t.Id, t.Status, t.CreatedAt, Lines = lines, Total = total });
}).RequireAuthorization();

// Tickets list (paged), sorted by CreatedAt DESC
app.MapGet("/api/tickets", async (TicketListQueryDto query, AppDbContext db) =>
{
    var page = query.Page ?? 1;
    var pageSize = query.PageSize ?? 20;
    pageSize = Math.Min(pageSize, 100);

    var baseQuery = db.Tickets.AsNoTracking().OrderByDescending(t => t.CreatedAt);
    var total = await baseQuery.CountAsync();
    var items = await baseQuery
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(t => new { t.Id, t.Status, t.CreatedAt })
        .ToListAsync();

    return Results.Ok(new { items, page, pageSize, total });
})
.WithValidator<TicketListQueryDto>()
.RequireAuthorization();

app.MapPost("/api/tickets/{id:guid}/lines", async (Guid id, AddLineDto dto, AppDbContext db) =>
{
    var ticket = await db.Tickets.FindAsync(id);
    if (ticket is null || ticket.Status != "Open")
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["ticketId"] = new[] { "Ticket invalid or not open." }
        });
    }

    var item = await db.Menu.FindAsync(dto.MenuItemId);
    if (item is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["menuItemId"] = new[] { "Menu item not found." }
        });
    }

    var existing = await db.TicketLines.FirstOrDefaultAsync(l => l.TicketId == id && l.MenuItemId == item.Id && l.UnitPrice == item.Price);
    if (existing is not null)
    {
        existing.Qty += dto.Qty;
    }
    else
    {
        db.TicketLines.Add(new TicketLine
        {
            TicketId = id,
            MenuItemId = item.Id,
            Qty = dto.Qty,
            UnitPrice = item.Price
        });
    }

    await db.SaveChangesAsync();
    return Results.Created($"/api/tickets/{id}", new { ok = true });
})
.WithValidator<AddLineDto>()
.RequireAuthorization();

app.MapPost("/api/tickets/{id:guid}/pay/cash", async (Guid id, AppDbContext db) =>
{
    var t = await db.Tickets.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
    if (t is null || t.Status != "Open")
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["ticketId"] = new[] { "Ticket invalid or not open." }
        });

    t.Status = "Paid";
    await db.SaveChangesAsync();

    var total = await db.TicketLines.Where(l => l.TicketId == id)
        .SumAsync(l => l.Qty * l.UnitPrice);

    return Results.Ok(new { t.Id, t.Status, Total = total });
}).RequireAuthorization();

// AUTH
app.MapPost("/api/auth/register", async (RegisterDto dto, AppDbContext db, IPasswordHasher<User> hasher) =>
{
    var email = dto.Email.Trim().ToLowerInvariant();

    var user = new User
    {
        Email = email,
        Role = string.IsNullOrWhiteSpace(dto.Role) ? "user" : dto.Role!.Trim()
    };

    user.PasswordHash = hasher.HashPassword(user, dto.Password);

    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email, user.Role });
})
.WithValidator<RegisterDto>();

app.MapPost("/api/auth/login", async (LoginDto dto, AppDbContext db, HttpContext http, IPasswordHasher<User> hasher) =>
{
    var email = dto.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null) return Results.Unauthorized();

    var result = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
    if (result == PasswordVerificationResult.Failed) return Results.Unauthorized();

    var jwtSection = app.Configuration.GetSection("Jwt");
    var issuer = jwtSection["Issuer"] ?? string.Empty;
    var audience = jwtSection["Audience"] ?? string.Empty;
    var signingKey = jwtSection["SigningKey"] ?? string.Empty;
    var ttlMinutes = int.TryParse(jwtSection["AccessTokenTTLMinutes"], out var ttl) ? ttl : 60;

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var now = DateTime.UtcNow;
    var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role)
    };

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: now,
        expires: now.AddMinutes(ttlMinutes),
        signingCredentials: creds
    );

    var jwt = new JwtSecurityTokenHandler().WriteToken(token);

    // Issue refresh token cookie (v1.1)
    var refreshSection = app.Configuration.GetSection("RefreshJwt");
    var rIssuer = refreshSection["Issuer"] ?? issuer;
    var rAudience = refreshSection["Audience"] ?? audience;
    var rSigningKey = refreshSection["SigningKey"] ?? string.Empty;
    var rTtlDays = int.TryParse(refreshSection["TTLDays"], out var rtd) ? rtd : 7;
    if (string.IsNullOrWhiteSpace(rSigningKey))
        throw new InvalidOperationException("RefreshJwt SigningKey is not configured. Set RefreshJwt__SigningKey env var.");
    var rKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(rSigningKey));
    var rCreds = new SigningCredentials(rKey, SecurityAlgorithms.HmacSha256);
    var rToken = new JwtSecurityToken(
        issuer: rIssuer,
        audience: rAudience,
        claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim("typ", "refresh") },
        notBefore: now,
        expires: now.AddDays(rTtlDays),
        signingCredentials: rCreds
    );
    var rJwt = new JwtSecurityTokenHandler().WriteToken(rToken);
    http.Response.Cookies.Append("refresh_token", rJwt, new CookieOptions
    {
        HttpOnly = true,
        Secure = !app.Environment.IsDevelopment(),
        SameSite = app.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
        Expires = DateTimeOffset.UtcNow.AddDays(rTtlDays),
        Path = "/"
    });

    return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = ttlMinutes * 60 });
})
.WithValidator<LoginDto>();

app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
{
    if (user?.Identity is null || !user.Identity.IsAuthenticated) return Results.Unauthorized();
    var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
    var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email);
    var role = user.FindFirstValue(ClaimTypes.Role) ?? "user";
    return Results.Ok(new { id = sub, email, role });
}).RequireAuthorization();

// Refresh access token using refresh cookie (v1.1)
app.MapPost("/api/auth/refresh", async (HttpContext http, AppDbContext db) =>
{
    if (!http.Request.Cookies.TryGetValue("refresh_token", out var refreshJwt) || string.IsNullOrWhiteSpace(refreshJwt))
        return Results.Unauthorized();

    var refreshSection = app.Configuration.GetSection("RefreshJwt");
    var rIssuer = refreshSection["Issuer"] ?? string.Empty;
    var rAudience = refreshSection["Audience"] ?? string.Empty;
    var rSigningKey = refreshSection["SigningKey"] ?? string.Empty;
    var rTtlDays = int.TryParse(refreshSection["TTLDays"], out var rtd) ? rtd : 7;
    if (string.IsNullOrWhiteSpace(rSigningKey)) return Results.Unauthorized();
    var tokenHandler = new JwtSecurityTokenHandler();
    try
    {
        var principal = tokenHandler.ValidateToken(refreshJwt, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = rIssuer,
            ValidateAudience = true,
            ValidAudience = rAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(rSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        }, out _);

        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub)) return Results.Unauthorized();
        if (!Guid.TryParse(sub, out var userId)) return Results.Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Results.Unauthorized();

        // Issue new access token
        var jwtSection2 = app.Configuration.GetSection("Jwt");
        var issuer = jwtSection2["Issuer"] ?? string.Empty;
        var audience = jwtSection2["Audience"] ?? string.Empty;
        var signingKey = jwtSection2["SigningKey"] ?? string.Empty;
        var ttlMinutes = int.TryParse(jwtSection2["AccessTokenTTLMinutes"], out var ttl) ? ttl : 60;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(ttlMinutes),
            signingCredentials: creds
        );
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        // Rotate refresh token
        var rKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(rSigningKey));
        var rCreds = new SigningCredentials(rKey, SecurityAlgorithms.HmacSha256);
        var rToken = new JwtSecurityToken(
            issuer: rIssuer,
            audience: rAudience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim("typ", "refresh") },
            notBefore: now,
            expires: now.AddDays(rTtlDays),
            signingCredentials: rCreds
        );
        var rJwtNew = new JwtSecurityTokenHandler().WriteToken(rToken);
        http.Response.Cookies.Append("refresh_token", rJwtNew, new CookieOptions
        {
            HttpOnly = true,
            Secure = !app.Environment.IsDevelopment(),
            SameSite = app.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(rTtlDays),
            Path = "/"
        });

        return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = ttlMinutes * 60 });
    }
    catch
    {
        return Results.Unauthorized();
    }
});

// Logout: clear refresh cookie
app.MapPost("/api/auth/logout", (HttpContext http) =>
{
    http.Response.Cookies.Append("refresh_token", "", new CookieOptions
    {
        HttpOnly = true,
        Secure = !app.Environment.IsDevelopment(),
        SameSite = app.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
        Expires = DateTimeOffset.UtcNow.AddDays(-1),
        Path = "/"
    });
    return Results.NoContent();
});

app.Run();// Updated Thu Sep 25 16:38:59 WIB 2025
