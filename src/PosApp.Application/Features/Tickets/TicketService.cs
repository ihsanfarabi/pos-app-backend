using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Domain.Entities;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Features.Tickets;

public sealed class TicketService(ITicketRepository ticketRepository, IMenuRepository menuRepository)
    : ITicketService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<Guid> CreateAsync(CancellationToken cancellationToken)
    {
        var ticket = Ticket.Create();
        await ticketRepository.AddAsync(ticket, cancellationToken);
        await ticketRepository.SaveChangesAsync(cancellationToken);
        return ticket.Id;
    }

    public async Task<TicketDetailsResponse?> GetAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetWithLinesAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        var menuMap = await menuRepository.GetByIdsAsync(ticket.Lines.Select(l => l.MenuItemId), cancellationToken);
        var lines = ticket.Lines
            .Select(l =>
            {
                var menu = menuMap.TryGetValue(l.MenuItemId, out var item) ? item : null;
                var name = menu?.Name ?? "Unknown";
                var total = l.Qty * l.UnitPrice;
                return new TicketLineResponse(l.Id, name, l.Qty, l.UnitPrice, total);
            })
            .ToList();

        var totalAmount = lines.Sum(x => x.LineTotal);
        return new TicketDetailsResponse(ticket.Id, ticket.Status, ticket.CreatedAt, lines, totalAmount);
    }

    public async Task<TicketListResult> GetListAsync(TicketListQueryDto query, CancellationToken cancellationToken)
    {
        var page = query.Page ?? 1;
        var pageSize = NormalizePageSize(query.PageSize);

        var pagedTickets = await ticketRepository.GetPagedAsync(page, pageSize, cancellationToken);
        var items = pagedTickets.Items
            .Select(t => new TicketListItemResponse(t.Id, t.Status, t.CreatedAt))
            .ToList();
        var pagination = new PaginationMetadata(pagedTickets.Page, pagedTickets.PageSize, pagedTickets.Total);
        return new TicketListResult(items, pagination);
    }

    public async Task AddLineAsync(Guid ticketId, AddLineDto dto, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetWithLinesAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            throw new NotFoundException("Ticket", ticketId.ToString());
        }

        var menuItem = await menuRepository.GetByIdAsync(dto.MenuItemId, cancellationToken);
        if (menuItem is null)
        {
            throw new ValidationException("Menu item not found.", "menuItemId");
        }

        try
        {
            ticket.AddLine(menuItem.Id, menuItem.Price, dto.Qty);
            await ticketRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            throw new ValidationException(ex.Message, ex.PropertyName);
        }
    }

    public async Task<TicketPaymentResponse> PayCashAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetWithLinesAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            throw new NotFoundException("Ticket", ticketId.ToString());
        }

        try
        {
            ticket.PayCash();
            await ticketRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            throw new ValidationException(ex.Message, ex.PropertyName);
        }

        var total = ticket.GetTotal();
        return new TicketPaymentResponse(ticket.Id, ticket.Status, total);
    }

    private static int NormalizePageSize(int? requested)
    {
        if (!requested.HasValue)
        {
            return DefaultPageSize;
        }

        var value = requested.Value;
        if (value <= 0)
        {
            throw new ValidationException("PageSize must be greater than zero.", "pageSize");
        }

        return Math.Min(value, MaxPageSize);
    }
}
