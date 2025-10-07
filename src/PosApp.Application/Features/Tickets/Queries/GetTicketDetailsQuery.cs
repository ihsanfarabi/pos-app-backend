using MediatR;
using PosApp.Application.Abstractions.Persistence;

namespace PosApp.Application.Features.Tickets.Queries;

public sealed record GetTicketDetailsQuery(Guid TicketId) : IRequest<TicketDetailsResponse?>;

public class GetTicketDetailsQueryHandler(
    ITicketRepository ticketRepository,
    IMenuRepository menuRepository)
    : IRequestHandler<GetTicketDetailsQuery, TicketDetailsResponse?>
{
    public async Task<TicketDetailsResponse?> Handle(GetTicketDetailsQuery request, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetWithLinesAsync(request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        var menuMap = await menuRepository.GetByIdsAsync(ticket.Lines.Select(l => l.MenuItemId), cancellationToken);
        var lines = ticket.Lines
            .Select(line =>
            {
                var menu = menuMap.TryGetValue(line.MenuItemId, out var item) ? item : null;
                var name = menu?.Name ?? "Unknown";
                var total = line.Qty * line.UnitPrice;
                return new TicketLineResponse(line.Id, name, line.Qty, line.UnitPrice, total);
            })
            .ToList();

        var totalAmount = lines.Sum(x => x.LineTotal);
        return new TicketDetailsResponse(ticket.Id, ticket.Status.ToString(), ticket.CreatedAt, lines, totalAmount);
    }
}
