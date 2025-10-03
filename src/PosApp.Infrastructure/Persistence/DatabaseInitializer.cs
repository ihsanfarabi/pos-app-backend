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
        await context.Database.MigrateAsync(cancellationToken);

        await SeedMenuAsync(context, cancellationToken);
        await SeedAdminUserAsync(scope.ServiceProvider, configuration, cancellationToken);
    }

    private static async Task SeedMenuAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Menu.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Menu.AddRange(
            new MenuItem { Name = "Nasi Goreng Ati Ampela", Price = 25000 },
            new MenuItem { Name = "Mie Goreng", Price = 22000 },
            new MenuItem { Name = "Teh Manis", Price = 8000 }
        );

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

        var admin = new User
        {
            Email = normalizedEmail,
            Role = "admin"
        };

        admin.PasswordHash = passwordHasher.HashPassword(admin, adminPassword);
        context.Users.Add(admin);
        await context.SaveChangesAsync(cancellationToken);
    }
}
