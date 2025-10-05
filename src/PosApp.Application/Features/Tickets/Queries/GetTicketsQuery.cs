using MediatR;
using FluentValidation;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Application.Contracts;

namespace PosApp.Application.Features.Tickets.Queries;

public sealed record GetTicketsQuery(TicketListQueryDto Query) : IRequest<PagedResult<TicketListItemResponse>>;

public sealed class GetTicketsQueryValidator : AbstractValidator<GetTicketsQuery>
{
    public GetTicketsQueryValidator()
    {
        RuleFor(x => x.Query.PageIndex)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Query.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100);
    }
}

internal sealed class GetTicketsQueryHandler(ITicketRepository ticketRepository)
    : IRequestHandler<GetTicketsQuery, PagedResult<TicketListItemResponse>>
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<PagedResult<TicketListItemResponse>> Handle(GetTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var pageIndex = Math.Max(0, query.PageIndex);
        var pageSize = NormalizePageSize(query.PageSize);

        var pagedTickets = await ticketRepository.GetPagedAsync(pageIndex, pageSize, cancellationToken);
        var items = pagedTickets.Items
            .Select(ticket => new TicketListItemResponse(ticket.Id, ticket.Status.ToString(), ticket.CreatedAt))
            .ToList();
        return new PagedResult<TicketListItemResponse>(items, pagedTickets.PageIndex, pagedTickets.PageSize, pagedTickets.TotalCount);
    }

    private static int NormalizePageSize(int requested)
    {
        if (requested <= 0)
        {
            return DefaultPageSize;
        }

        return Math.Min(requested, MaxPageSize);
    }
}
