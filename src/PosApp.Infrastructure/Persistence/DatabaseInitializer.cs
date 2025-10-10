using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PosApp.Application.Abstractions.Security;
using PosApp.Domain.Entities;

namespace PosApp.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitialiseAsync(IServiceProvider serviceProvider, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var applyMigrations = configuration.GetValue("Database:ApplyMigrations", true);
        if (applyMigrations)
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        var seedEnabled = configuration.GetValue("Database:Seed", false);
        if (seedEnabled)
        {
            await SeedMenuAsync(context, cancellationToken);
            await SeedAdminUserAsync(scope.ServiceProvider, configuration, cancellationToken);
        }
    }

    private static async Task SeedMenuAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Menu.AnyAsync(cancellationToken))
        {
            return;
        }

        var menuItems = new[]
        {
            MenuItem.Create("Nasi Goreng Ati Ampela", 25000),
            MenuItem.Create("Mie Goreng", 22000),
            MenuItem.Create("Teh Manis", 8000)
        };

        context.Menu.AddRange(menuItems);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAdminUserAsync(IServiceProvider serviceProvider, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();

        var adminEmail = configuration["Admin:Email"] ?? "admin@example.com";
        var adminPassword = configuration["Admin:Password"] ?? "ChangeMe123!";
        var normalizedEmail = adminEmail.Trim().ToLowerInvariant();

        if (await context.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            return;
        }

        var admin = User.Create(normalizedEmail, UserRoles.Admin);
        var passwordHash = passwordHasher.HashPassword(admin, adminPassword);
        admin.SetPasswordHash(passwordHash);
        context.Users.Add(admin);
        await context.SaveChangesAsync(cancellationToken);
    }
}
