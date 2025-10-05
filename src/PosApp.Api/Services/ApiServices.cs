using MediatR;

namespace PosApp.Api.Services;

public sealed class MenuServices(
    ISender sender,
    ILogger<MenuServices> logger)
{
    public ISender Sender { get; } = sender;
    public ILogger<MenuServices> Logger { get; } = logger;
}

public sealed class TicketServices(
    ISender sender,
    ILogger<TicketServices> logger)
{
    public ISender Sender { get; } = sender;
    public ILogger<TicketServices> Logger { get; } = logger;
}

public sealed class AuthServices(
    ISender sender,
    ILogger<AuthServices> logger)
{
    public ISender Sender { get; } = sender;
    public ILogger<AuthServices> Logger { get; } = logger;
}


