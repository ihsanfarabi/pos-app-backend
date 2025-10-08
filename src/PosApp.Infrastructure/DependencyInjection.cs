using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Abstractions.Security;
using PosApp.Domain.Entities;
using PosApp.Infrastructure.Options;
using PosApp.Infrastructure.Persistence;
using PosApp.Infrastructure.Persistence.Repositories;
using PosApp.Infrastructure.Security;
using PosApp.Infrastructure.Behaviors;
using MediatR;
using PosApp.Application.Abstractions.Payments;
using PosApp.Infrastructure.Payments;

namespace PosApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<RefreshJwtOptions>(configuration.GetSection(RefreshJwtOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string 'Default' is not configured.");
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            if (IsSqliteConnectionString(connectionString))
            {
                options.UseSqlite(connectionString);
                options.ConfigureWarnings(builder => builder.Ignore(RelationalEventId.PendingModelChangesWarning));
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        });

        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<ITokenService, TokenService>();

        // Payment gateway (mock)
        services.AddSingleton<IPaymentGateway, MockPaymentGateway>();

        // MediatR transaction behavior for commands
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }

    private static bool IsSqliteConnectionString(string connectionString)
    {
        return connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            || connectionString.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase);
    }
}
