using Microsoft.EntityFrameworkCore;
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

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

// Enable CORS for frontend dev server
app.UseCors();

// MENU
app.MapGet("/api/menu", async (AppDbContext db) =>
    await db.Menu.OrderBy(m => m.Name)
        .Select(m => new { m.Id, m.Name, m.Price })
        .ToListAsync());

app.MapPost("/api/menu", async (AppDbContext db, CreateMenuItemDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name) || dto.Price <= 0)
        return Results.BadRequest(new { message = "Invalid name/price" });

    var item = new MenuItem { Name = dto.Name.Trim(), Price = dto.Price };
    db.Menu.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/menu/{item.Id}", new { item.Id });
});

// TICKETS
app.MapPost("/api/tickets", async (AppDbContext db) =>
{
    var t = new Ticket();
    db.Tickets.Add(t);
    await db.SaveChangesAsync();
    return Results.Created($"/api/tickets/{t.Id}", new { t.Id });
});

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
});

app.MapPost("/api/tickets/{id:guid}/lines", async (Guid id, AddLineDto dto, AppDbContext db) =>
{
    var t = await db.Tickets.FindAsync(id);
    if (t is null || t.Status != "Open")
        return Results.BadRequest(new { message = "Ticket invalid or not open" });

    var item = await db.Menu.FindAsync(dto.MenuItemId);
    if (item is null || dto.Qty < 1)
        return Results.BadRequest(new { message = "Invalid item/qty" });

    db.TicketLines.Add(new TicketLine
    {
        TicketId = id,
        MenuItemId = item.Id,
        Qty = dto.Qty,
        UnitPrice = item.Price
    });

    await db.SaveChangesAsync();
    return Results.Created($"/api/tickets/{id}", new { ok = true });
});

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
});

app.Run();// Updated Thu Sep 25 16:38:59 WIB 2025
