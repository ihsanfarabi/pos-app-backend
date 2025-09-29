using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Identity;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using PosApi.Contracts;
using PosApi.Domain;
using PosApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Db
var connectionString = builder.Configuration.GetConnectionString("Default") ?? string.Empty;
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(connectionString);
});

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
              .AllowAnyMethod());
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

// Swagger
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

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

// Enable CORS for frontend dev server
app.UseCors();

// AuthZ
app.UseAuthentication();
app.UseAuthorization();

// MENU
app.MapGet("/api/menu", async (AppDbContext db) =>
    await db.Menu.OrderBy(m => m.Name)
        .Select(m => new { m.Id, m.Name, m.Price })
        .ToListAsync())
    .RequireAuthorization();

app.MapPost("/api/menu", async (AppDbContext db, CreateMenuItemDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name) || dto.Price <= 0)
        return Results.BadRequest(new { message = "Invalid name/price" });

    var item = new MenuItem { Name = dto.Name.Trim(), Price = dto.Price };
    db.Menu.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/menu/{item.Id}", new { item.Id });
}).RequireAuthorization("Admin");

app.MapPut("/api/menu/{id:guid}", async (Guid id, UpdateMenuItemDto dto, AppDbContext db) =>
{
    var item = await db.Menu.FindAsync(id);
    if (item is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(dto.Name) || dto.Price <= 0)
        return Results.BadRequest(new { message = "Invalid name/price" });

    item.Name = dto.Name.Trim();
    item.Price = dto.Price;
    await db.SaveChangesAsync();
    return Results.Ok(new { item.Id });
}).RequireAuthorization("Admin");

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

app.MapPost("/api/tickets/{id:guid}/lines", async (Guid id, AddLineDto dto, AppDbContext db) =>
{
    var t = await db.Tickets.FindAsync(id);
    if (t is null || t.Status != "Open")
        return Results.BadRequest(new { message = "Ticket invalid or not open" });

    var item = await db.Menu.FindAsync(dto.MenuItemId);
    if (item is null || dto.Qty < 1)
        return Results.BadRequest(new { message = "Invalid item/qty" });

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
}).RequireAuthorization();

app.MapPost("/api/tickets/{id:guid}/pay/cash", async (Guid id, AppDbContext db) =>
{
    var t = await db.Tickets.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
    if (t is null || t.Status != "Open")
        return Results.BadRequest(new { message = "Ticket invalid or not open" });

    t.Status = "Paid";
    await db.SaveChangesAsync();

    var total = await db.TicketLines.Where(l => l.TicketId == id)
        .SumAsync(l => l.Qty * l.UnitPrice);

    return Results.Ok(new { t.Id, t.Status, Total = total });
}).RequireAuthorization();

// AUTH
app.MapPost("/api/auth/register", async (RegisterDto dto, AppDbContext db, IPasswordHasher<User> hasher) =>
{
    if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest(new { message = "Email and password required" });

    if (!dto.Email.Contains('@'))
        return Results.BadRequest(new { message = "Email is invalid" });

    if (dto.Password.Length < 8)
        return Results.BadRequest(new { message = "Password must be at least 8 characters" });

    var email = dto.Email.Trim().ToLowerInvariant();
    var exists = await db.Users.AnyAsync(u => u.Email == email);
    if (exists) return Results.BadRequest(new { message = "Email already registered" });

    var user = new User { Email = email, Role = string.IsNullOrWhiteSpace(dto.Role) ? "user" : dto.Role!.Trim() };
    user.PasswordHash = hasher.HashPassword(user, dto.Password);

    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email, user.Role });
});

app.MapPost("/api/auth/login", async (LoginDto dto, AppDbContext db) =>
{
    var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest(new { message = "Email and password required" });
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null) return Results.Unauthorized();

    using var scope = app.Services.CreateScope();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    var result = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password ?? string.Empty);
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
    return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = ttlMinutes * 60 });
});

app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
{
    if (user?.Identity is null || !user.Identity.IsAuthenticated) return Results.Unauthorized();
    var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
    var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email);
    var role = user.FindFirstValue(ClaimTypes.Role) ?? "user";
    return Results.Ok(new { id = sub, email, role });
}).RequireAuthorization();

app.Run();// Updated Thu Sep 25 16:38:59 WIB 2025
