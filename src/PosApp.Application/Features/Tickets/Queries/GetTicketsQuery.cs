using MediatR;
using FluentValidation;
using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Application.Contracts;
using PosApp.Application.Exceptions;

namespace PosApp.Application.Features.Tickets.Queries;

public sealed record GetTicketsQuery(TicketListQueryDto Query) : IRequest<TicketListResult>;

public sealed class GetTicketsQueryValidator : AbstractValidator<GetTicketsQuery>
{
    public GetTicketsQueryValidator()
    {
        RuleFor(x => x.Query.Page)
            .GreaterThan(0)
            .When(x => x.Query.Page.HasValue);

        RuleFor(x => x.Query.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .When(x => x.Query.PageSize.HasValue);
    }
}

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
            return DefaultPageSize;
        }

        return Math.Min(value, MaxPageSize);
    }
}
