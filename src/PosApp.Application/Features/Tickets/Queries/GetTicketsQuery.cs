using MediatR;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;
using PosApp.Application.Features.Tickets;

namespace PosApp.Application.Features.Tickets.Queries;

public sealed record GetTicketsQuery(TicketListQueryDto Query) : IRequest<TicketListResult>;

internal sealed class GetTicketsQueryHandler(ITicketRepository ticketRepository)
    : IRequestHandler<GetTicketsQuery, TicketListResult>
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<TicketListResult> Handle(GetTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var page = query.Page ?? 1;
        var pageSize = NormalizePageSize(query.PageSize);

        var pagedTickets = await ticketRepository.GetPagedAsync(page, pageSize, cancellationToken);
        var items = pagedTickets.Items
            .Select(ticket => new TicketListItemResponse(ticket.Id, ticket.Status.ToString(), ticket.CreatedAt))
            .ToList();
        var pagination = new PaginationMetadata(pagedTickets.Page, pagedTickets.PageSize, pagedTickets.Total);
        return new TicketListResult(items, pagination);
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
