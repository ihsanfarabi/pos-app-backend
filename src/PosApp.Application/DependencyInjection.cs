using Microsoft.Extensions.DependencyInjection;
using PosApp.Application.Features.Auth;
using PosApp.Application.Features.Menu;
using PosApp.Application.Features.Tickets;

namespace PosApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
